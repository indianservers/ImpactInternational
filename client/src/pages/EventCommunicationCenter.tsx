import {useState,type ReactNode} from 'react';
import {useQueryClient} from '@tanstack/react-query';
import {Mail,MessageSquare,Users,UserCheck,Send,Eye,Clock,CheckCircle2} from 'lucide-react';
import {api} from '../api';
import {useGov,ErrorBox} from '../GovernanceUI';
import EventHierarchy from './EventHierarchy';
import './EventCommunicationCenter.css';

export default function EventCommunicationCenter({id,event,onAudience,legacy}:{id:string;event:any;onAudience:()=>void;legacy:ReactNode}){
 const qc=useQueryClient(),[org,setOrg]=useState(''),[segment,setSegment]=useState('All'),[action,setAction]=useState('invite'),[page,setPage]=useState(1);
 const [message,setMessage]=useState(`You are invited to ${event.title}. Join your Impact International community. Please RSVP and register to reserve your place.`),[schedule,setSchedule]=useState('');
 const [review,setReview]=useState(false),[busy,setBusy]=useState(false),[error,setError]=useState<unknown>(),[result,setResult]=useState(''),[request,setRequest]=useState(()=>crypto.randomUUID());
 const effectiveSegment=action==='invite'?'All':segment;
 const params=new URLSearchParams({segment:effectiveSegment,page:String(page),...(org?{orgId:org}:{})});
 const q=useGov('/events/'+id+'/communication?'+params),d=q.data;
 const publishable=event.publication==='Published'&&event.approval==='Approved'&&!['Cancelled','Completed','Archived'].includes(event.lifecycle)&&new Date(event.endsAt)>new Date();
 const count=action==='invite'?d?.newRecipients:d?.linked;
 const when=new Date(event.startsAt).toLocaleString(undefined,{dateStyle:'medium',timeStyle:'short',timeZone:event.timeZone});
 function resetReview(){setReview(false);setResult('');setRequest(crypto.randomUUID());}
 async function send(){setBusy(true);setError(undefined);try{
  const r=action==='invite'?await api('/events/'+id+'/invitations','POST',{message,sendAt:schedule?new Date(schedule).toISOString():null,...(org?{orgId:org}:{})}):await api('/events/'+id+'/member-updates','POST',{requestId:request,message,orgId:org||null,segment});
  setResult(`${r.queued} ${action==='invite'?'new invitations':'linked member updates'} queued. Processing is not proof of email delivery.`);setReview(false);qc.invalidateQueries({queryKey:['gov']});
 }catch(e){setError(e)}finally{setBusy(false)}}
 return <div className="ec-center"><header className="ec-hero"><div><p>EVENT COMMUNICATION DESK</p><h2>Reach the right members. Bring your community together.</h2><span>District → Zone → Club · approved event audience · deduplicated recipients</span></div><MessageSquare size={42}/></header>
  <div className="ec-metrics">{[[Users,'Eligible members',d?.total],[Mail,'Invited',d?.invited],[Clock,'Awaiting RSVP',d?.awaiting],[UserCheck,'Confirmed',d?.confirmed]].map(([Icon,label,value]:any)=><article key={label}><Icon size={20}/><strong>{value??'—'}</strong><span>{label}</span></article>)}</div>
  <EventHierarchy title="Recipient unit" value={org} onChange={value=>{setOrg(value);setPage(1);resetReview();}}/>
  <div className="ec-layout"><section className="ec-panel"><header><h3><Send size={17}/> Compose communication</h3><button className="secondary" onClick={onAudience}>Audience settings</button></header>
   {!publishable&&<p className="ec-info">Preview is available now. Invitations and updates require an approved, published event that has not ended.</p>}
   <label>Communication purpose<select aria-label="Event communication purpose" value={action} onChange={e=>{setAction(e.target.value);setPage(1);resetReview();}}><option value="invite">Invite new eligible members</option><option value="update">Send event update or reminder</option></select></label>
   {action==='update'&&<label>Send to<select aria-label="Event update audience" value={segment} onChange={e=>{setSegment(e.target.value);setPage(1);resetReview();}}>{['All','Awaiting RSVP','Confirmed','Not registered'].map(x=><option key={x}>{x}</option>)}</select></label>}
   <div className="ec-template-actions"><span>Quick message:</span><button className="secondary" onClick={()=>{setAction('invite');setPage(1);setMessage(`You are invited to ${event.title} on ${when} (${event.timeZone}). Venue: ${event.venue||'Online'}. Please RSVP and register to reserve your place.`);resetReview();}}>Invitation</button><button className="secondary" onClick={()=>{setAction('update');setSegment('Awaiting RSVP');setMessage(`A friendly reminder to respond to ${event.title}. Date: ${when} (${event.timeZone}). Venue: ${event.venue||'Online'}. RSVP and registration are separate steps.`);setPage(1);resetReview();}}>RSVP reminder</button><button className="secondary" onClick={()=>{setAction('update');setSegment('Confirmed');setMessage(`We look forward to seeing you at ${event.title}. ${when} (${event.timeZone}), ${event.venue||'Online'}. Open the event for the latest agenda and your registration details.`);setPage(1);resetReview();}}>Joining details</button></div>
   <label>Message<textarea aria-label="Event communication message" rows={6} minLength={5} maxLength={2000} value={message} onChange={e=>{setMessage(e.target.value);resetReview();}}/></label><small>{message.length}/2,000 characters</small>
   {action==='invite'&&<label>Schedule invitation delivery (optional, your local time)<input aria-label="Schedule event invitations" type="datetime-local" value={schedule} onChange={e=>{setSchedule(e.target.value);resetReview();}}/></label>}
   <div className="ec-channel-note"><MessageSquare size={16}/><span>In-app notifications for linked accounts. Email uses configured delivery and member preferences. WhatsApp/SMS are not connected.</span></div>
   <ErrorBox error={error||q.error}/>{result&&<p role="status" className="success"><CheckCircle2 size={16}/>{result}</p>}
   {review?<div className="ec-review" role="region" aria-label="Review event communication"><h4>Review before queuing</h4><p><strong>{count||0}</strong> {action==='invite'?'new eligible members':'linked members'} · {effectiveSegment}</p><p className="pre-line">{message}</p>{schedule&&action==='invite'&&<small>Scheduled: {new Date(schedule).toLocaleString()}</small>}<p>Recipients remain within the event’s approved audience. Private event links are added automatically.</p><div><button disabled={busy} onClick={send}>{busy?'Queuing…':'Confirm and queue'}</button><button className="secondary" disabled={busy} onClick={()=>setReview(false)}>Edit message</button></div></div>:<button disabled={!publishable||q.isFetching||!count||message.trim().length<5||busy||!!result} onClick={()=>setReview(true)}><Eye size={16}/>Preview {count??0} recipients & message</button>}
  </section><section className="ec-panel"><header><h3><Users size={17}/> Recipient preview</h3><span>{d?.total??0} eligible</span></header><p>{action==='invite'?`${d?.newRecipients??0} not previously invited. Existing invitations are not resent.`:`${d?.linked??0} linked accounts can receive this update.`}</p>{d?.unlinked>0&&<p className="ec-info">{d.unlinked} members have no linked login account. Updates cannot reach those accounts until linked.</p>}
   {q.isPending?<p role="status">Checking the approved audience…</p>:<div className="ec-recipient-list">{d?.items?.map((p:any)=><article key={p.id}><span className="ec-avatar">{p.displayName.split(' ').map((x:string)=>x[0]).slice(0,2).join('')}</span><div><strong>{p.displayName}</strong><small>{p.clubs.join(' · ')||'Approved event audience'}</small><span>{p.rsvp||'Not invited'} · {p.registration||'Not registered'}</span></div><b>{p.linked?'Linked':'Unlinked'}</b></article>)}{!d?.items?.length&&<p>No eligible members match this unit and segment.</p>}</div>}
   <footer><button className="secondary" disabled={page===1} onClick={()=>setPage(page-1)}>Previous</button><span>Page {page}</span><button className="secondary" disabled={page*30>=(d?.total||0)} onClick={()=>setPage(page+1)}>Next</button></footer>
  </section></div>
  {d?.jobs?.length>0&&<section className="ec-panel"><h3>Member update processing</h3>{d.jobs.map((j:any)=><div className="ec-job" key={j.id}><p>{j.message}</p><span>{j.completed?'Processed':'Queued / processing'}</span></div>)}<small>Processed means the notification job completed. It does not confirm email delivery or that a member read the message.</small></section>}
  <p className="ec-definition">{d?.notice}</p><details className="ec-panel"><summary>Advanced guest invitations & email delivery log</summary>{legacy}</details>
 </div>;
}
