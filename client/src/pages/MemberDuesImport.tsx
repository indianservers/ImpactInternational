import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { Download, Upload } from "lucide-react";
import { api, apiForm } from "../api";
import { ErrorBox, useWorkspace } from "../App";

export function ImportTemplates({kind}:{kind:"members"|"dues"}) {
  return <section className="panel spaced"><h2>Download a {kind === "members" ? "member" : "dues"} import template</h2>
    <div className="header-actions spaced">{([['csv',false,'Blank CSV'],['xlsx',false,'Blank Excel'],['csv',true,'Sample CSV'],['xlsx',true,'Sample Excel']] as const).map(([format,sample,label]) =>
      <a key={label} className="button secondary" href={`/api/v1/membership/import-template?kind=${kind}&format=${format}&sample=${sample}`} download><Download size={15}/>{label}</a>)}</div>
    <p>{kind === "members" ? "Name is required. Blank club and category codes use the defaults selected below. Dates use YYYY-MM-DD. Replace or delete the sample row before importing. Import dues after members have received their membership numbers." : "Use membership numbers from the Members export. Required columns: key, memberNumber, amount, date, dueOn. Keep each key unchanged on retries. Amounts use the selected financial entity’s currency. Dates use YYYY-MM-DD."}</p>
  </section>;
}

export function ImportAudit() {
  const q=useQuery<any[]>({queryKey:['import-audit'],queryFn:()=>api('/membership/import-audit'),refetchInterval:10000});
  return <section className="panel spaced"><h2>Import audit trail</h2><p>Uploader, file fingerprint, validation, duplicate decisions and final results are recorded. Most recent 100 events in your authorized scope.</p><ErrorBox error={q.error}/>
    <details><summary>View {q.data?.length || 0} recent audit events</summary><div className="table-scroll"><table><thead><tr><th>When</th><th>Officer</th><th>Action</th><th>Import / entity</th><th>Details</th></tr></thead><tbody>{q.data?.map(r=><tr key={r.id}><td>{new Date(r.at).toLocaleString()}</td><td>{r.actor}</td><td>{r.action}</td><td>{r.resource}</td><td><span style={{overflowWrap:'anywhere'}}>{r.changes}</span></td></tr>)}</tbody></table></div></details>
  </section>;
}

function report(rows:any[]) {
  const quote=(value:unknown)=>{let s=String(value??'');if(/^[\s]*[=+\-@]/.test(s))s="'"+s;return '"'+s.replaceAll('"','""')+'"';};
  const data=[['row','key','memberNumber','member','amount','status','error'],...rows.map(r=>[r.input.row,r.input.key,r.input.memberNumber,r.member,r.input.amount,r.status,r.error])];
  const url=URL.createObjectURL(new Blob(['\uFEFF'+data.map(r=>r.map(quote).join(',')).join('\r\n')],{type:'text/csv'}));
  const a=document.createElement('a');a.href=url;a.download='dues-import-review.csv';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
}

export default function MemberDuesImport() {
  const {w}=useWorkspace();const qc=useQueryClient();
  const [entity,setEntity]=useState(''),[file,setFile]=useState<File|null>(null),[preview,setPreview]=useState<any>(),[result,setResult]=useState<any>(),[error,setError]=useState<unknown>(),[busy,setBusy]=useState(false),[ack,setAck]=useState(false);
  const allowed=!!w?.permissions.includes('finance.manage');
  const entities=useQuery<any[]>({queryKey:['dues-import-entities',w?.id],queryFn:()=>api('/membership/dues-imports/entities'),enabled:allowed});
  const reset=()=>{setPreview(undefined);setResult(undefined);setError(undefined);setAck(false);};
  return <><ImportTemplates kind="dues"/><section className="panel spaced"><h2>Bulk import member dues</h2>
    <p>Preview up to 500 rows from a CSV or Excel file. All rows must pass scope, amount, date and duplicate checks before confirmation. The import creates invoice drafts in one transaction. Review and issue them in Finance; paid amounts must use verified receipts.</p>
    {!allowed?<p className="privacy-notice">Dues import requires both member import permission and financial billing authority. Ask your financial administrator to use an authorized workspace.</p>:<>
    <ErrorBox error={error||entities.error}/><form className="form-stack" onSubmit={async e=>{e.preventDefault();if(!file||!entity)return;setBusy(true);reset();try{const data=new FormData();data.append('file',file);data.append('entityId',entity);setPreview(await apiForm('/membership/dues-imports/preview',data));await qc.invalidateQueries({queryKey:['import-audit']});}catch(e){setError(e);}finally{setBusy(false);}}}>
      <label>Financial entity<select required value={entity} disabled={busy} onChange={e=>{setEntity(e.target.value);reset();}}><option value="">Choose an authorized financial entity</option>{entities.data?.map(e=><option key={e.id} value={e.id}>{e.name} · {e.currency}</option>)}</select></label>
      {!entities.isPending&&entities.data?.length===0&&<p>No financial entities grant you billing authority. Configure the entity and its served clubs in Finance first.</p>}
      <label>Dues file (CSV or Excel, up to 1 MB)<input type="file" accept=".csv,.xlsx" required disabled={busy} onChange={e=>{setFile(e.target.files?.[0]||null);reset();}}/></label>
      <button disabled={busy||!file||!entity}><Upload size={15}/>{busy?'Checking rows…':'Validate and preview dues'}</button>
    </form>
    {preview&&<div className="spaced"><h3>Review {preview.total} dues rows</h3><p role="status">{preview.ready} ready · {preview.duplicates} already imported · {preview.errors} errors · Currency: {preview.currency}</p>
      <button className="secondary" onClick={()=>report(preview.rows)}><Download size={15}/>Download full review report</button>
      {preview.errors>0&&<p className="error">Correct the reported errors and upload again. No invoices have been created.</p>}
      <div className="table-scroll"><table><thead><tr><th>File row</th><th>Key / member number</th><th>Member</th><th>Amount</th><th>Dates</th><th>Status / validation</th></tr></thead><tbody>{preview.rows.map((r:any)=><tr key={r.input.row}><td>{r.input.row}</td><td>{r.input.key}<br/>{r.input.memberNumber}</td><td>{r.member||'—'}</td><td>{r.input.amount} {preview.currency}</td><td>{r.input.date}<br/>Due {r.input.dueOn}</td><td>{r.status}<br/>{r.error}</td></tr>)}</tbody></table></div>
      {preview.token&&!result&&<><label className="checkbox spaced"><input type="checkbox" checked={ack} disabled={busy} onChange={e=>setAck(e.target.checked)}/>I reviewed these charges and confirm the selected financial entity and amounts.</label><button disabled={busy||!ack} onClick={async()=>{setBusy(true);setError(undefined);try{setResult(await api('/membership/dues-imports/commit','POST',{token:preview.token}));await qc.invalidateQueries({queryKey:['import-audit']});await qc.invalidateQueries({queryKey:['finance']});}catch(e){setError(e);}finally{setBusy(false);}}}>{busy?'Importing…':`Confirm ${preview.ready} dues drafts`}</button></>}
    </div>}
    {result&&<div className="success spaced" role="status"><b>{result.created} draft invoices created · {result.reused} existing invoices safely reused.</b><p>No charges were issued or payments marked paid.</p><Link to="/app/finance">Review in Finance</Link></div>}
    </>}
  </section><ImportAudit/></>;
}
