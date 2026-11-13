import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { ErrorBox } from "../App";
import { useFinance } from "./FinanceContext";
type Row=Record<string,any>;
function csv(text:string):string[][] {
  const rows:string[][]=[];let row:string[]=[],value="",quoted=false;
  for(let i=0;i<text.length;i++){const c=text[i];if(c==='"'){if(quoted&&text[i+1]==='"'){value+='"';i++;}else quoted=!quoted;}else if(c===','&&!quoted){row.push(value);value="";}else if((c==='\n'||c==='\r')&&!quoted){if(c==='\r'&&text[i+1]==='\n')i++;row.push(value);value="";if(row.some(Boolean))rows.push(row);row=[];}else value+=c;}
  if(quoted)throw new Error("Unclosed CSV quote.");row.push(value);if(row.some(Boolean))rows.push(row);return rows;
}
export default function FinanceInvoiceImport(){
  const {path}=useFinance();const qc=useQueryClient();const [invoices,setInvoices]=useState<Row[]>([]),[results,setResults]=useState<Row[]>([]),[error,setError]=useState<unknown>(),[busy,setBusy]=useState(false);
  async function read(file:File){setError(undefined);setInvoices([]);setResults([]);try{
    if(file.size>500000)throw new Error("Use a CSV or JSON file smaller than 500 KB.");
    const text=(await file.text()).replace(/^\uFEFF/,"");let data:Row[];
    if(file.name.toLowerCase().endsWith(".json"))data=JSON.parse(text);
    else {const rows=csv(text),headers=rows.shift()?.map(h=>h.trim())||[];for(const h of ["key","customer","date","dueOn","description","rate"])if(!headers.includes(h))throw new Error("Missing column: "+h);
      data=rows.map(values=>{const r=Object.fromEntries(headers.map((h,i)=>[h,values[i]||""]));return {key:r.key,customer:r.customer,purpose:r.purpose||"Other",date:r.date,dueOn:r.dueOn,notes:r.notes||"",personId:r.personId||null,membershipId:r.membershipId||null,lines:[{description:r.description,quantity:r.quantity||"1",rate:r.rate,accountCode:r.accountCode||"4000"}]};});}
    if(!Array.isArray(data)||!data.length||data.length>500)throw new Error("Import 1–500 invoices at a time.");
    if(data.some(r=>!r.key||!r.customer||!r.date||!r.dueOn||!Array.isArray(r.lines)||!r.lines.length))throw new Error("Each invoice needs a stable key, customer, issue date, due date, and lines.");
    if(new Set(data.map(r=>r.key)).size!==data.length)throw new Error("Import keys must be unique within the file.");setInvoices(data);
  }catch(error){setError(error);}}
  return <section className="fc-card"><h2>Import invoice drafts</h2><p>CSV columns: key, customer, date, dueOn, description, rate. Optional: purpose, quantity, accountCode, personId, membershipId, notes. JSON can include multiple lines. Use existing member IDs to link dues; imports never create members.</p><p>Stable keys make retries safe. Imported invoices remain drafts for review before issuing or notifying anyone.</p><input aria-label="Invoice import file" type="file" accept=".csv,.json" disabled={busy} onChange={e=>{const file=e.target.files?.[0];if(file)read(file);}}/><ErrorBox error={error}/>{invoices.length>0&&<><p><b>{invoices.length} invoices ready for review</b></p><div className="fc-table-scroll"><table className="fin-table"><thead><tr><th>Key</th><th>Customer</th><th>Issue date</th><th>Due date</th><th>Lines</th></tr></thead><tbody>{invoices.slice(0,10).map(r=><tr key={r.key}><td>{r.key}</td><td>{r.customer}</td><td>{r.date}</td><td>{r.dueOn}</td><td>{r.lines.length}</td></tr>)}</tbody></table></div><button className="fc-primary" disabled={busy} onClick={async()=>{setBusy(true);setResults([]);const output:Row[]=[];for(const invoice of invoices){try{const result=await api<Row>(path+"/invoices","POST",invoice);output.push({key:invoice.key,success:true,id:result.id});}catch(error){output.push({key:invoice.key,success:false,error:error instanceof Error?error.message:String(error)});}setResults([...output]);}await qc.invalidateQueries({queryKey:["finance"]});setBusy(false);}}>Confirm draft import</button></>}{results.length>0&&<div role="status"><p>{results.filter(r=>r.success).length} drafts created or safely reused · {results.filter(r=>!r.success).length} failed · {results.length}/{invoices.length} processed</p>{results.filter(r=>!r.success).map(r=><p key={r.key}>{r.key}: {r.error}</p>)}</div>}</section>;
}
