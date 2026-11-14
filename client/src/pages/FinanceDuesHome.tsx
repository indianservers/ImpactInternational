import {useState, type ReactNode} from 'react';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {ChevronDown, ChevronRight, RefreshCw} from 'lucide-react';
import {api} from '../api';
import {ErrorBox, useWorkspace} from '../App';
import {CollectionForm} from './DuesReports';
import './FinanceDuesHome.css';
import FinanceMemberIntelligence from './FinanceMemberIntelligence';

type Row = Record<string, any>;
const base = '/finance/dues-center';
const money = (value: number, currency: string) => new Intl.NumberFormat('en-IN', {style:'currency', currency}).format(value || 0);
const levels = ['National', 'Region', 'District', 'Zone', 'Club'];

export default function FinanceDuesHome({advanced}: {advanced: ReactNode}) {
  const {w} = useWorkspace(), qc = useQueryClient();
  const [selection, setSelection] = useState(['','','','','']);
  const [view, setView] = useState({level:'Club', orgId:''});
  const [mode, setMode] = useState('dues'), [page, setPage] = useState(1), [collectionStatus, setCollectionStatus] = useState('Verified');
  const [from, setFrom] = useState(''), [to, setTo] = useState(''), [currency, setCurrency] = useState('');
  const [expanded, setExpanded] = useState(''), [collection, setCollection] = useState<Row | null>();
  const [showTools, setShowTools] = useState(false);
  const options = useQuery<Row>({queryKey:['dues-options',w?.id], queryFn:()=>api(base+'/options')});
  const orgs: Row[] = options.data?.organizations || [];
  const params = new URLSearchParams({page:String(page), sort:'pending', ...(view.orgId?{orgId:view.orgId}:{}), ...(from?{from}:{}), ...(to?{to}:{}), ...(currency?{currency}:{})});
  const valid = !from || !to || from <= to;
  const kind = view.level.toLowerCase();
  const dues = useQuery<Row>({queryKey:['simple-dues',w?.id,kind,params.toString()], queryFn:()=>api(base+'/reports/'+kind+'?'+params), enabled:valid});
  const collectionParams=new URLSearchParams(params); collectionParams.set('status',collectionStatus);
  const collections = useQuery<Row>({queryKey:['simple-collections',w?.id,collectionParams.toString()], queryFn:()=>api(base+'/collections?'+collectionParams), enabled:valid&&mode==='collections'});
  function under(id: string, parent: string) {
    const seen = new Set<string>();
    while (id && !seen.has(id)) {if(id===parent)return true;seen.add(id);id=orgs.find(o=>o.id===id)?.parentId||'';}
    return false;
  }
  function select(index: number, id: string) {setSelection(selection.map((v,i)=>i<index?v:i===index?id:''));}
  function show(level: string) {
    const index=levels.indexOf(level), orgId=selection.slice(0,index+1).reverse().find(Boolean)||'';
    setView({level,orgId}); setMode('dues'); setPage(1); setExpanded('');
  }
  function reset() {setSelection(['','','','','']);setView({level:'Club',orgId:''});setFrom('');setTo('');setCurrency('');setCollectionStatus('Verified');setPage(1);setExpanded('');setMode('dues');}
  function refresh() {qc.invalidateQueries({queryKey:['member-finance-intelligence']});qc.invalidateQueries({queryKey:['member-finance-ledger']});qc.invalidateQueries({queryKey:['simple-dues']});qc.invalidateQueries({queryKey:['simple-pending']});qc.invalidateQueries({queryKey:['simple-collections']});qc.invalidateQueries({queryKey:['dues-report']});}
  const scopeName = orgs.find(o=>o.id===view.orgId)?.name || 'All units in your authorized scope';
  const totals: Row[] = dues.data?.summary || [];
  const selectedClub=view.level==='Club'&&orgs.find(o=>o.id===view.orgId)?.type==='Club';
  const currencies = Array.from(new Set<string>([...totals.map(r=>r.currency),...(options.data?.plans||[]).map((r:Row)=>r.currency)]));
  return <div className="simple-finance">
    {options.data?.mockData&&<p className="dues-mock-banner">Mock data · No real funds</p>}
    <header className="sf-heading"><div><p>IMPACT INTERNATIONAL / FINANCE</p><h1>Dues & Collections</h1><span>Choose a club to see every member, payment status, charts and connected payment history.</span></div><button className="secondary" aria-label="Refresh dues and collections" onClick={refresh}><RefreshCw size={16}/>Refresh</button></header>
    <section className="sf-scope" aria-label="Find dues by organization">
      <div className="sf-quick-filters">{levels.slice(2).map(level=>{
        const i=levels.indexOf(level), parent=selection.slice(0,i).reverse().find(Boolean);
        return <div className="sf-select-action" key={level}><label>{level}<select aria-label={'Dues '+level.toLowerCase()} value={selection[i]} onChange={e=>select(i,e.target.value)}><option value="">All {level.toLowerCase()}s{parent?' in selected parent':''}</option>{orgs.filter(o=>o.type===level&&(!parent||under(o.id,parent))).map(o=><option key={o.id} value={o.id}>{o.name}</option>)}</select></label><button onClick={()=>show(level)}>Dues by {level.toLowerCase()}</button></div>;
      })}</div>
      <details className="sf-extra"><summary>Region, national unit & date filters</summary><div className="sf-extra-fields">{levels.slice(0,2).map((level,i)=><label key={level}>{level}<select aria-label={'Dues '+level.toLowerCase()} value={selection[i]} onChange={e=>select(i,e.target.value)}><option value="">All {level.toLowerCase()}s</option>{orgs.filter(o=>o.type===level&&(!selection[0]||i===0||under(o.id,selection[0]))).map(o=><option key={o.id} value={o.id}>{o.name}</option>)}</select></label>)}<label>Invoices from<input aria-label="Dues invoices from" type="date" value={from} onChange={e=>{setFrom(e.target.value);setPage(1);setExpanded('');}}/></label><label>Through<input aria-label="Dues through" type="date" value={to} onChange={e=>{setTo(e.target.value);setPage(1);setExpanded('');}}/></label><label>Currency<select aria-label="Dues currency" value={currency} onChange={e=>{setCurrency(e.target.value);setPage(1);setExpanded('');}}><option value="">All · separate totals</option>{currencies.map(c=><option key={c}>{c}</option>)}</select></label></div><small>Leave dates blank to include all dues. Date filters use invoice dates; collection records use payment dates.</small></details>
      <div className="sf-showing"><span><strong>Showing:</strong> {scopeName} · grouped by {view.level.toLowerCase()}</span><button className="text-button" onClick={reset}>Clear filters</button></div>
    </section>
    {!valid&&<p role="alert">The start date must be before the end date.</p>}
    <ErrorBox error={options.error||dues.error}/>
    {dues.isPending&&<p role="status">Loading dues…</p>}
    {!selectedClub&&<div className="sf-totals">{totals.map(t=><section key={t.currency}><h2>{t.currency}</h2><div><span>Pending dues<strong>{money(t.pending,t.currency)}</strong></span><span>Collected against dues<strong>{money(t.collected,t.currency)}</strong></span><span>Overdue<strong>{money(t.overdue,t.currency)}</strong></span></div>{t.pendingVerification>0&&<p>{money(t.pendingVerification,t.currency)} received, awaiting verification. Still included in pending dues.</p>}</section>)}</div>}
    <div className="sf-actions"><button aria-pressed={mode==='dues'} onClick={()=>{setMode('dues');setPage(1);}}>Members &amp; dues</button><button aria-pressed={mode==='collections'} onClick={()=>{setMode('collections');setPage(1);}}>Check collections</button>{options.data?.canCollect&&<button className="secondary" onClick={()=>setCollection(null)}>Record a collection</button>}</div>
    <section className="sf-results" aria-label={mode==='dues'?'Dues results':'Collection results'}><header><div><h2>{mode==='dues'?view.level+' dues':'Collections received'}</h2><p>{scopeName}</p></div><a className="button secondary" href={'/api/v1'+base+(mode==='dues'?'/reports/'+kind:'/collections')+'?'+(mode==='dues'?params:collectionParams)+'&format=csv'}>Export CSV</a></header>
      {mode==='dues'?(selectedClub?<FinanceMemberIntelligence key={view.orgId+from+to+currency} orgId={view.orgId} currency={currency} from={from} to={to} canCollect={options.data?.canCollect} onCollect={setCollection}/>:<>
        <div className="sf-group-labels"><span>{view.level}</span><span>Collected</span><span>Pending dues</span></div>
        {(dues.data?.items||[]).map((r:Row)=>{const key=r.groupId+':'+r.currency,open=expanded===key;return <article className="sf-group" key={key}><button className="sf-group-toggle" aria-expanded={open} aria-label={(open?'Hide':'Show')+' pending members for '+r.group+' '+r.currency} onClick={()=>setExpanded(open?'':key)}><span>{open?<ChevronDown size={17}/>:<ChevronRight size={17}/>}<span><strong>{r.group}</strong><small>{r.members} members with dues · {r.currency}</small></span></span><span>{money(r.collected,r.currency)}</span><span><strong>{money(r.pending,r.currency)}</strong><small>{r.pending>0?'View pending members':'No pending dues'}</small></span></button>{open&&<FinanceMemberIntelligence key={key+from+to} orgId={r.groupId} currency={r.currency} from={from} to={to} canCollect={options.data?.canCollect} onCollect={setCollection}/>}</article>})}
        {!dues.isPending&&!dues.data?.items?.length&&<p className="sf-empty">No dues found for this selection. Try another club or clear the filters.</p>}
        <Pager page={page} total={dues.data?.total||0} onChange={setPage}/>
      </>):<><label className="sf-collection-status">Show collections<select aria-label="Collection status" value={collectionStatus} onChange={e=>{setCollectionStatus(e.target.value);setPage(1);}}>{[['Verified','Verified payments'],['Submitted','Awaiting verification'],['Rejected','Rejected entries'],['Cancelled','Cancelled entries'],['Refunded','Refunded payments'],['','All entries']].map(([value,label])=><option key={value} value={value}>{label}</option>)}</select></label><ErrorBox error={collections.error}/>{collections.isPending?<p role="status">Loading collections…</p>:<div className="sf-table"><table><thead><tr>{['Member','Club','Date','Method','Amount','Status','Reference'].map(x=><th key={x}>{x}</th>)}</tr></thead><tbody>{collections.data?.items?.map((r:Row)=><tr key={r.id}><td>{r.member||'—'}</td><td>{r.club||'—'}</td><td>{r.date}</td><td>{r.method}</td><td>{money(r.amount,r.currency)}</td><td>{r.paymentId?(r.status==='Unsettled'?'Verified · bank settlement pending':r.status==='Settled'?'Verified · settled':r.status):r.status==='Submitted'?'Awaiting verification':r.status}</td><td>{r.receipt||r.reference||'—'}</td></tr>)}</tbody></table>{!collections.data?.items?.length&&<p className="sf-empty">No collections found for this selection.</p>}</div>}<Pager page={page} total={collections.data?.total||0} onChange={setPage}/><p className="sf-note">Submitted payments are awaiting verification. Only verified payments reduce pending dues. This list may contain separate allocations from one payment.</p></>}
    </section>
    <button className="secondary sf-more" aria-expanded={showTools} onClick={()=>setShowTools(!showTools)}>{showTools?'Hide':'More'} finance tools</button>
    {showTools&&<section className="sf-advanced"><h2>Detailed finance tools</h2><p>Use these for dues generation, reconciliation, adjustments and accounting.</p>{advanced}</section>}
    {collection!==undefined&&<CollectionForm initialInvoice={collection?{id:collection.invoiceId,currency:collection.currency,entity:collection.entity,customer:collection.member,number:collection.invoice,club:collection.club,dueOn:collection.dueOn,outstanding:collection.pending,pendingVerification:collection.pendingVerification}:undefined} onClose={()=>setCollection(undefined)} onSaved={()=>{setCollection(undefined);refresh();}}/>}
  </div>;
}

function Pager({page,total,onChange}:{page:number;total:number;onChange:(page:number)=>void}) {
  return <footer className="sf-pager"><span>{total} records{total>0?' · Page '+page:''}</span><button className="secondary" disabled={page===1} onClick={()=>onChange(page-1)}>Previous</button><button className="secondary" disabled={page*50>=total} onClick={()=>onChange(page+1)}>Next</button></footer>;
}
