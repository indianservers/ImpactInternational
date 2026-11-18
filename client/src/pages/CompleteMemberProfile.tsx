import {MemberParticipationHistory} from '../ParticipationManager';
import { BusinessProfileCard } from "../BusinessProfile";
import {MemberCertifications} from "./Certifications";
import {useQuery} from '@tanstack/react-query';
import {Link} from 'react-router-dom';
import {api} from '../api';
import {useWorkspace,ErrorBox,Skeleton} from '../App';
import {MemberAvatar} from '../MemberComponents';
import {MemberOffices} from '../GovernanceUI';
import './CompleteMemberProfile.css';
type Row=Record<string,any>;
const text=(value:any)=>Array.isArray(value)?value.join(', '):value===true?'Yes':value===false?'No':String(value??'');
const label=(key:string)=>key.replace(/([A-Z])/g,' $1').replace(/^./,c=>c.toUpperCase());
function Fields({items}:{items:[string,any][]}){return <dl className="cmp-fields">{items.map(([name,value],i)=><div key={name+':'+i}><dt>{name}</dt><dd>{text(value)||'Not recorded or not shared'}</dd></div>)}</dl>}
function Section({title,children}:{title:string;children:React.ReactNode}){return <section className="cmp-section"><h3>{title}</h3>{children}</section>}
function Records({rows,empty}:{rows:Row[];empty:string}){return rows.length?<div className="cmp-records">{rows.map((r,i)=><Fields key={r.id||i} items={Object.entries(r).filter(([k,v])=>!k.toLowerCase().endsWith('id')&&!['version','tenantId','actorId','requestKey'].includes(k)&&v!=null&&typeof v!=='object'&&k!=='hasPhoto').map(([k,v])=>[label(k),/At$/.test(k)&&v&&!Number.isNaN(Date.parse(String(v)))?new Date(String(v)).toLocaleString():v])}/>)}</div>:<p className="cmp-empty">{empty}</p>}
export default function CompleteMemberProfile({membershipId,data,directory=false,leadershipData}:{membershipId?:string;data?:Row;directory?:boolean;leadershipData?:Row}){
 const {w}=useWorkspace();
 const q=useQuery<Row>({queryKey:['member-profile',membershipId,directory,w?.id],queryFn:()=>api('/members/'+membershipId+(directory?'?directory=true':'')),enabled:!data&&!!membershipId&&!!w});
 const d=data||q.data,p=d?.person,m=d?.membership;
 const hierarchy=useQuery<Row>({queryKey:['profile-hierarchy',w?.id,m?.clubId],queryFn:()=>api('/membership/hierarchy?clubId='+m.clubId),enabled:!!m?.clubId&&!!w});
 if(!data&&q.isPending)return <Skeleton/>;
 if(q.error&&!data)return <ErrorBox error={q.error}/>;
 if(!p)return <p>No profile is available in this scope.</p>;
 return <div className="complete-member-profile" role="region" aria-label="Complete member profile">
  <header className="cmp-identity">{membershipId&&<MemberAvatar id={membershipId} name={p.name} photo={p.hasPhoto} directory={directory} size="xl"/>}<div><span>IMPACT INTERNATIONAL · COMPLETE PROFILE</span><h2>{p.name}</h2><p>{m?.number||p.reference} {m?.club?' · '+m.club:''}</p></div>{m?.status&&<b>{m.status}</b>}</header>
  <p className="cmp-notice">Only information returned by your authorized workspace is shown. Missing and restricted fields are not inferred.</p>
  {!directory&&<MemberCertifications personId={p.id} editable={!!w?.permissions.includes("members.manage")}/>}
  <Section title="Personal information"><Fields items={[
   ['Full legal name',p.legalName],['Display name',p.displayName||p.name],['Person reference',p.reference],['Date of birth',p.birthDate],['Gender',p.gender],['Marital status',p.maritalStatus],['Spouse name',p.spouseName],['Marriage anniversary',p.anniversary],['Languages',p.languages],['Education level',p.educationLevel],['Qualification',p.qualification],['Institution',p.institution],['Employment status',p.employmentStatus],['Additional information',p.additionalInformation]
  ]}/></Section>
  <Section title="Contact information & emergency contacts">{p.contacts?.length?<Fields items={p.contacts.map((c:Row)=>[c.kind+' · '+(c.label||'Contact')+(c.primary?' · Primary':''),c.value])}/>:<p className="cmp-empty">No contacts are recorded or shared with this workspace.</p>}</Section>
  <Section title="Addresses">{p.addresses?.length?<div className="cmp-addresses">{p.addresses.map((a:Row,i:number)=><div key={a.id||i}><strong>{a.kind||a.type||a.label||'Address'}{a.isPrimary?' · Primary':''}</strong><p>{[a.address,a.city,a.state,a.postalCode,a.country].filter(Boolean).join(', ')}</p></div>)}</div>:<p className="cmp-empty">No addresses are recorded or shared.</p>}</Section>
  <BusinessProfileCard professional={p.professional}/>
  <MemberParticipationHistory personId={p.id}/>
  <Section title="Professional profile & interests">{p.professional?<Fields items={Object.entries(p.professional).map(([k,v])=>[label(k),v])}/>:<p className="cmp-empty">Professional information is not recorded or not shared.</p>}</Section>
  <Section title="Social profiles">{p.socialLinks&&Object.keys(p.socialLinks).length?<div className="cmp-social">{Object.entries(p.socialLinks).map(([name,url])=>/^https?:\/\//i.test(String(url))?<a key={name} href={String(url)} target="_blank" rel="noopener noreferrer">{name} ↗</a>:<span key={name}>{name}: {String(url)}</span>)}</div>:<p className="cmp-empty">No shared social profiles.</p>}</Section>
  <Section title="Membership & organizational hierarchy">{m&&<Fields items={[
   ['Membership ID',m.number],['Club',m.club],['Category',m.category],['Status',m.status],['Joined',m.joinedAt?new Date(m.joinedAt).toLocaleDateString():null],['Valid till',m.endsAt?new Date(m.endsAt).toLocaleDateString():'While active'],['Renewal status',m.renewalStatus]
  ]}/>}<ErrorBox error={hierarchy.error}/>{hierarchy.isPending&&m?.clubId?<p>Loading organizational hierarchy…</p>:<ol className="cmp-hierarchy">{[...(hierarchy.data?.selectedPath||[])].reverse().map((o:Row)=><li key={o.id}><small>{o.kind}</small><strong>{o.name}</strong></li>)}</ol>}
  {(d.memberships||[]).map((a:Row)=><Link className="cmp-affiliation" key={a.id} to={'/app/members/'+a.id+(directory?'?directory=true':'?tab=Complete+profile')}><strong>{a.club}</strong><span>{a.number} · {a.category} · {a.status}</span></Link>)}</Section>
  <Section title="Roles & leadership assignments">{membershipId&&(!directory||p.privacy!=null)&&!leadershipData&&<MemberOffices id={membershipId}/>}<Records rows={d.roles||[]} empty="No roles are visible in this scope."/>{(d.appointments||leadershipData?.appointments)&&<Records rows={d.appointments||leadershipData?.appointments} empty="No leadership assignments recorded."/>}</Section>
  {leadershipData?.committees&&<Section title="Committee assignments"><Records rows={leadershipData.committees} empty="No committee assignments visible in this scope."/></Section>}
  <Section title="Membership history & renewals"><Records rows={d.history||[]} empty="No membership history is recorded or shared."/><Records rows={d.renewals||[]} empty="No recorded renewals."/></Section>
  <Section title="Applications & transfers"><Records rows={[...(d.applications||[]),...(d.transfers||[])]} empty="No applications or transfers are visible in this scope."/></Section>
  <Section title="Documents">{d.canDocuments&&d.documents?.length?<div className="cmp-records">{d.documents.map((doc:Row)=><a key={doc.id} href={'/api/v1/membership/documents/'+doc.id} download>{doc.fileName} · {doc.kind} · {doc.status}</a>)}</div>:<p className="cmp-empty">No documents are available with your current permissions.</p>}</Section>
  <Section title="Activity & role history"><Records rows={[...(d.activities||[]),...(d.roleHistory||[])]} empty="No activity or role changes are visible in this scope."/></Section>
 </div>;
}

export function LeadershipCompleteProfile({personId,personData}:{personId:string;personData?:Row}){
 const {w}=useWorkspace();
 const q=useQuery<Row>({queryKey:['complete-person',w?.id,personId],queryFn:()=>api('/leadership/people/'+personId),enabled:!personData&&!!personId&&!!w});
 const d=personData||q.data;
 if(!personData&&q.isPending)return <Skeleton/>;
 if(q.error&&!personData)return <ErrorBox error={q.error}/>;
 if(!d)return null;
 const member=d.affiliations?.find((m:Row)=>m.status==='Active')||d.affiliations?.[0];
 return member&&w?.permissions.includes('members.view')?<CompleteMemberProfile key={member.id} membershipId={member.id} leadershipData={d}/>:<CompleteMemberProfile data={{person:d.profile,memberships:[],roles:[],appointments:d.appointments,history:[],activities:d.history}}/>;
}
