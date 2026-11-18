import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { X } from 'lucide-react';
import { api } from './api';
import { ErrorBox, useWorkspace } from './App';
import { ActivityGallery, ActivityFilePicker } from './ActivityMedia';
import {uploadActivityMedia} from './activityUploads';
import type { MediaUpload } from './activityUploads';
import { useDialog } from './useDialog';
type Row = Record<string, any>;

export default function PostEditor({ value, spaces, categories, close, saved }: { value: Row; spaces: Row[]; categories: string[]; close: () => void; saved: () => void }) {
  const { w,me } = useWorkspace(), qc = useQueryClient();
  const initialLevel = spaces.find(s => s.id === value.orgId)?.level;
  const [form, setForm] = useState<Row>(() => ({ title: '', body: '', category: 'Club activity', activityOn: new Date().toLocaleDateString('en-CA'), participants: 0, volunteerHours: 0, location: '', linkUrl: '', pinned: false, requestKey: crypto.randomUUID(), version: 1, visibility: ['Club','Region','National'].includes(initialLevel) ? initialLevel : 'Public', ...value }));
  const [busy, setBusy] = useState(false), [error, setError] = useState<unknown>(), [tasks, setTasks] = useState<MediaUpload[]>([]), [published, setPublished] = useState(false);
  const policy = useQuery<Row>({queryKey:['community-policy',w?.id],queryFn:()=>api('/community/policy')});
  const events = useQuery<Row[]>({queryKey:['community-events',w?.id],queryFn:()=>api('/community/events'),enabled:!value.id});
  const [memberSearch,setMemberSearch]=useState('');
  const members=useQuery<Row[]>({queryKey:['community-tag-options',w?.id,form.orgId,memberSearch],queryFn:()=>api('/community/participant-options?'+new URLSearchParams({orgId:form.orgId,search:memberSearch})),enabled:!!form.orgId});
  async function importEvent(eventId:string){if(!eventId){setForm(current=>({...current,sourceEventId:undefined,participantIds:[]}));return;}setBusy(true);setError(undefined);try{const draft=await api('/community/events/'+eventId+'/draft');setForm(current=>({...current,...draft,sourceEventId:eventId,visibility:['Club','Region','National'].includes(spaces.find(s=>s.id===draft.orgId)?.level)?spaces.find(s=>s.id===draft.orgId)?.level:'Public'}));}catch(e){setError(e);}finally{setBusy(false);}}
  const storage = useQuery<Row>({ queryKey: ['community-storage', w?.id], queryFn: () => api('/community/media-storage') });
  useDialog(true, () => { if (!busy) close(); });
  const field = (key: string, value: any) => setForm({ ...form, [key]: value });
  const orgLevel = spaces.find(s => s.id === form.orgId)?.level;
  const audiences = orgLevel === 'Club' ? ['Club','Region','National','Public'] : orgLevel === 'Region' ? ['Region','National','Public'] : orgLevel === 'National' ? ['National','Public'] : ['Public'];
  const patchTask = (key: string, patch: Partial<MediaUpload>) => setTasks(current => current.map(t => t.key === key ? { ...t, ...patch } : t));
  async function submit(e: React.FormEvent) {
    e.preventDefault(); setBusy(true); setError(undefined);
    try {
      const result = await api('/community/posts' + (form.id ? '/' + form.id : ''), form.id ? 'PUT' : 'POST', { ...form, participants: Number(form.participants), volunteerHours: Number(form.volunteerHours) });
      const id = result.id; setForm(current => ({ ...current, id, version: result.version })); setPublished(true);
      await qc.invalidateQueries({ queryKey: ['community'] });
      if (form.sourceEventId) await api('/community/posts/'+id+'/import-event-media','POST',{});
      for (const task of tasks.filter(t => t.status !== 'Uploaded')) {
        try { await uploadActivityMedia(id, task, patch => patchTask(task.key, patch)); }
        catch (e) { patchTask(task.key, { status: 'Failed', error: e instanceof Error ? e.message : String(e) }); throw new Error('Activity saved. A media upload failed. Keep this editor open and use Save & retry uploads to continue.'); }
      }
      saved();
    } catch (e) { setError(e); } finally { setBusy(false); }
  }
  return <div className="cm-modal-backdrop" onClick={() => { if (!busy) close(); }}><section className="cm-editor" role="dialog" aria-modal="true" aria-label={form.id ? 'Edit activity update' : 'New activity update'} onClick={e => e.stopPropagation()}>
    <div className="cm-editor-title"><h2>{form.id ? 'Edit activity update' : 'Share an activity update'}</h2><button className="secondary" disabled={busy} aria-label="Close update editor" onClick={close}><X size={18}/></button></div><ErrorBox error={error || storage.error}/>
    <form onSubmit={submit}><fieldset className="cm-editor-fields" disabled={busy}>
      {!form.id&&<section className="growth-event-import"><label>Start from a completed event<select value={form.sourceEventId||''} onChange={e=>importEvent(e.target.value)}><option value="">Write a new activity update</option>{events.data?.map(e=><option key={e.id} value={e.id}>{e.title}</option>)}</select></label><p className="cm-helper">Copies the event title, description, date, venue, coordinates, attendance count and checked-in members. Event gallery photos can be imported after the activity is saved.</p></section>}
      <label>Community space<select required value={form.orgId} disabled={!!form.id} onChange={e => { const org = spaces.find(s => s.id === e.target.value); setForm({ ...form, orgId: e.target.value, participantIds:[],visibility: ['Club','Region','National'].includes(org?.level) ? org?.level : 'Public' }); }}>{spaces.filter(s => form.id ? s.id === form.orgId : s.canPublish).map(s => <option key={s.id} value={s.id}>{s.level} · {s.name}</option>)}</select></label>
      <label>Who can see this activity?<select value={form.visibility} onChange={e => field('visibility', e.target.value)}>{form.visibility === 'Hierarchy' && <option value="Hierarchy">Organization & leadership (existing audience)</option>}{audiences.map(v => <option key={v} value={v}>{v === 'Public' ? 'Public · anyone, including visitors' : 'Only ' + v.toLowerCase() + ' · members and authorized leadership'}</option>)}</select></label>
      <p className="cm-helper">Presidents retain oversight of activities in the units they lead. Public activities and their media can be viewed without signing in.</p>
      {form.visibility==='Public'&&policy.data?.requirePublicApproval&&!me.platformAdmin&&<p className="growth-review-status">This public update will be submitted to parent-unit leadership for approval. It stays private until approved.</p>}
      <label>Title<input required minLength={3} maxLength={180} value={form.title} onChange={e => field('title', e.target.value)} placeholder="What happened in your community?"/></label>
      <label>Activity update<textarea required minLength={5} maxLength={10000} value={form.body} onChange={e => field('body', e.target.value)} placeholder="Share the story, outcomes and highlights…"/></label>
      <details><summary>Tag participating members ({form.participantIds?.length||0})</summary><p className="cm-helper">Members must accept their participation tags. Tagging alone does not verify attendance or issue a certificate.</p><label>Find participants<input maxLength={100} value={memberSearch} onChange={e=>setMemberSearch(e.target.value)} placeholder="Search member name or number"/></label><ErrorBox error={members.error}/><div className="growth-tags-picker">{members.data?.map(m=><label key={m.id}><input type="checkbox" checked={(form.participantIds||[]).includes(m.id)} onChange={e=>field('participantIds',e.target.checked?[...(form.participantIds||[]),m.id]:(form.participantIds||[]).filter((id:string)=>id!==m.id))}/>{m.name} · {m.number}</label>)}</div></details>
      <details><summary>Activity map coordinates (optional)</summary><p className="cm-helper">Without exact coordinates, discovery uses the organizational location when available.</p><div className="cm-form-grid"><label>Latitude<input type="number" step="any" min={-90} max={90} value={form.latitude??''} onChange={e=>field('latitude',e.target.value===''?null:Number(e.target.value))}/></label><label>Longitude<input type="number" step="any" min={-180} max={180} value={form.longitude??''} onChange={e=>field('longitude',e.target.value===''?null:Number(e.target.value))}/></label></div></details>
      <p className="cm-helper">Removing an attachment hides it from the feed. The private Google Drive copy is retained for recovery.</p>
      <ActivityGallery media={form.media} canRemove onRemove={async id => { try { await api('/community/media/' + id, 'DELETE'); setForm(current => ({ ...current, media: current.media.filter((m: Row) => m.id !== id) })); await qc.invalidateQueries({ queryKey: ['community'] }); } catch (e) { setError(e); } }}/>
      <ActivityFilePicker tasks={tasks} setTasks={setTasks} busy={busy} available={!!storage.data?.available} maxFiles={20-(form.media?.length||0)}/>
      <div className="cm-form-grid"><label>Category<select value={form.category} onChange={e => field('category', e.target.value)}>{categories.map(c => <option key={c}>{c}</option>)}</select></label><label>Activity date<input type="date" required value={form.activityOn} onChange={e => field('activityOn', e.target.value)}/></label><label>Recorded participants<input type="number" min={0} max={1000000} step={1} value={form.participants} onChange={e => field('participants', e.target.value)}/></label><label>Volunteer hours<input type="number" min={0} max={1000000} step="0.01" value={form.volunteerHours} onChange={e => field('volunteerHours', e.target.value)}/></label></div>
      <label>Location<input maxLength={200} value={form.location} onChange={e => field('location', e.target.value)}/></label><label>Activity link (optional)<input type="url" maxLength={1000} value={form.linkUrl} onChange={e => field('linkUrl', e.target.value)} placeholder="https://…"/></label><label className="cm-checkbox"><input type="checkbox" checked={form.pinned} onChange={e => field('pinned', e.target.checked)}/>Pin this update in the feed</label>
      <p className="cm-helper">Publish only media you have permission to share with the selected audience. Participant counts are recorded attendances, not unique member counts.</p>
    </fieldset><div className="cm-editor-buttons"><button disabled={busy}>{busy ? 'Saving and uploading…' : published ? 'Save & retry uploads' : form.id ? 'Save update' : form.visibility==='Public'&&policy.data?.requirePublicApproval&&!me.platformAdmin?'Submit for review':'Publish update'}</button><button type="button" className="secondary" disabled={busy} onClick={close}>{published ? 'Close saved activity' : 'Cancel'}</button></div></form>
  </section></div>;
}
