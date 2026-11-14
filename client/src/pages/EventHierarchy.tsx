import {useGov} from '../GovernanceUI';
import {ErrorBox} from '../App';
import './EventCommunicationCenter.css';
export default function EventHierarchy({value,onChange,title='Events by district, zone & club'}:{value:string;onChange:(id:string)=>void;title?:string}){
 const q=useGov('/event-hierarchy'),orgs:any[]=q.data||[],levels=['International','National','Region','District','Zone','Club'];
 const path:Record<string,string>={};let cursor=value;const seen=new Set<string>();
 while(cursor&&!seen.has(cursor)){seen.add(cursor);const node=orgs.find(o=>o.id===cursor);if(!node)break;path[node.type]=node.id;cursor=node.parentId;}
 function below(id:string,parent:string){const visited=new Set<string>();while(id&&!visited.has(id)){if(id===parent)return true;visited.add(id);id=orgs.find(o=>o.id===id)?.parentId;}return false;}
 return <section className="ec-hierarchy" aria-label={title}><header><strong>{title}</strong>{value&&<button type="button" className="text-button" onClick={()=>onChange('')}>Clear unit</button>}</header><div>{levels.map((level,i)=>{const parent=levels.slice(0,i).reverse().map(l=>path[l]).find(Boolean),items=orgs.filter(o=>o.type===level&&(!parent||below(o.id,parent)));return <label key={level}>{level}<select aria-label={title+' '+level.toLowerCase()} value={path[level]||''} disabled={q.isPending||!items.length} onChange={e=>onChange(e.target.value||parent||'')}><option value="">All authorized {level==='International'?'international units':level.toLowerCase()+'s'}{parent?' below selected parent':''}</option>{items.map(o=><option key={o.id} value={o.id}>{o.name}</option>)}</select></label>})}</div><small>{orgs.find(o=>o.id===value)?.name||'Your authorized hierarchy'} · Includes units below the selection</small><ErrorBox error={q.error}/></section>;
}
