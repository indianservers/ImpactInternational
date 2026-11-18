import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Cloud, ExternalLink, ImagePlus, ShieldCheck, Unplug, X } from 'lucide-react';
import { api } from './api';
import { ErrorBox, PageHeader, Skeleton, useWorkspace } from './App';
import type {MediaUpload} from './activityUploads';
import './ActivityMedia.css';

type Row = Record<string, any>;
export function ActivityGallery({ media = [], publicView = false, canRemove = false, onRemove }: { media?: Row[]; publicView?: boolean; canRemove?: boolean; onRemove?: (id: string) => void }) {
  const [failed, setFailed] = useState<Record<string, boolean>>({});
  if (!media.length) return null;
  return <div className="activity-gallery" aria-label="Activity photos and videos">{media.map(m => {
    const src = '/api/v1/' + (publicView ? 'public/activity-media/' : 'community/media/') + m.id + '/content';
    return <figure key={m.id}>{failed[m.id] ? <div className="activity-media-unavailable"><Cloud size={24}/><p>Media is currently unavailable.</p><a href={src} target="_blank" rel="noopener noreferrer">Try opening attachment</a></div> : m.mimeType.startsWith('video/') ? <video onError={() => setFailed(current => ({ ...current, [m.id]: true }))} controls preload="metadata" src={src} aria-label={m.fileName}>Your browser cannot play this video. <a href={src}>Open video</a></video> : <a href={src} target="_blank" rel="noopener noreferrer"><img src={src} alt={m.fileName} loading="lazy" onError={() => setFailed(current => ({ ...current, [m.id]: true }))} /></a>}<figcaption><span>{m.fileName}</span>{canRemove && <button type="button" className="secondary" aria-label={'Remove media ' + m.fileName} onClick={() => onRemove?.(m.id)}><X size={14}/></button>}</figcaption></figure>;
  })}</div>;
}

export function ActivityFilePicker({ tasks, setTasks, busy, available, maxFiles = 20 }: { tasks: MediaUpload[]; setTasks: (tasks: MediaUpload[]) => void; busy: boolean; available: boolean; maxFiles?: number }) {
  const [error, setError] = useState('');
  return <section className="activity-media-picker"><div className="activity-media-heading"><ImagePlus size={21}/><div><strong>Photos & videos</strong><p>One Google Drive folder per activity. Media uploads directly to Google.</p></div></div>
    {!available && <p className="activity-storage-notice">Platform Admin needs to connect Google Drive before you can attach media. You can still publish a text update.</p>}
    <label>Add photos or videos<input type="file" multiple disabled={busy || !available} accept="image/jpeg,image/png,image/webp,image/gif,video/mp4,video/webm,video/quicktime" onChange={e => {
      setError(''); const selected = Array.from(e.target.files || []); const next = [...tasks];
      for (const file of selected) {
        if (next.length >= maxFiles) { setError('An activity can have up to 20 attachments, including its existing media.'); break; }
        if (file.size <= 0 || file.size > (file.type.startsWith('video/') ? 500 : 20) * 1024 * 1024) { setError('Images must be up to 20 MB and videos up to 500 MB.'); continue; }
        if (!['image/jpeg','image/png','image/webp','image/gif','video/mp4','video/webm','video/quicktime'].includes(file.type)) { setError('Choose JPG, PNG, WEBP, GIF, MP4, WEBM or MOV files.'); continue; }
        next.push({ key: crypto.randomUUID(), file, progress: 0, status: 'Queued' });
      }
      setTasks(next); e.target.value = '';
    }}/></label>
    <small>Images: 20 MB each · Videos: 500 MB each · Up to 20 files. Uploads use the connected Google account’s storage.</small>
    {error && <p role="alert">{error}</p>}
    <div className="activity-upload-list">{tasks.map(task => <div key={task.key}><div><strong>{task.file.name}</strong><small>{(task.file.size / 1024 / 1024).toFixed(1)} MB · {task.status} {task.status === 'Uploading' ? task.progress + '%' : ''}</small>{task.error && <span role="alert">{task.error}</span>}<progress max={100} value={task.progress} aria-label={'Upload progress for ' + task.file.name}/></div>{!busy && task.status !== 'Uploaded' && <button type="button" className="secondary" aria-label={'Remove selected file ' + task.file.name} onClick={async () => {
      try { if (task.session) await api('/community/media/' + task.session.id, 'DELETE'); setTasks(tasks.filter(t => t.key !== task.key)); } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    }}><X size={14}/></button>}</div>)}</div>
  </section>;
}

export function GoogleMediaSettings() {
  const { w, me } = useWorkspace(), qc = useQueryClient(); const [params] = useSearchParams();
  const q = useQuery<Row>({ queryKey: ['google-media', w?.id], queryFn: () => api('/google-media/accounts'), enabled: !!me.platformAdmin });
  const [error, setError] = useState<unknown>(), [busy, setBusy] = useState(''), [confirm, setConfirm] = useState('');
  async function action(path: string) { setBusy(path); setError(undefined); try { await api(path, 'POST', {}); await qc.invalidateQueries({ queryKey: ['google-media'] }); await qc.invalidateQueries({ queryKey: ['community-storage'] }); setConfirm(''); } catch (e) { setError(e); } finally { setBusy(''); } }
  if (!me.platformAdmin) return <ErrorBox error="Only Platform Admin can manage Google storage accounts."/>;
  return <><PageHeader title="Google media storage" eyebrow="PLATFORM ADMINISTRATION" description="Connect administrator-owned Google accounts for activity photos and videos."><Link className="button secondary" to="/app/community">Activity feed</Link></PageHeader><ErrorBox error={error || q.error}/>
    {params.get('google') === 'connected' && <p className="success" role="status">Google account connected. Activities can now upload media.</p>}{params.get('google') === 'cancelled' && <p role="status">Google connection was cancelled. No account was added.</p>}
    {q.isPending ? <Skeleton/> : q.data && <div className="google-storage-layout"><section className="panel google-storage-intro"><Cloud size={36}/><h2>Activity albums, stored in Drive</h2><p>Each activity gets its own folder in the connected account. Photos and videos go directly from the uploader’s browser to Google Drive. The app keeps activity text and file references.</p><p>Drive files stay private. The app checks the activity’s audience whenever someone views media, so changing visibility also changes access.</p><div className="activity-storage-notice"><ShieldCheck size={19}/><span>We request access to files created by this app and your Google email. Your other Drive files remain outside this integration.</span></div>
      {!q.data.configured && <div className="activity-config"><h3>Google setup required</h3><p>Configure the Google OAuth web client on the server, enable Google Drive API, and register this exact redirect URI:</p><code>{q.data.redirectUri}</code><p>Required settings: GoogleMedia:ClientId, GoogleMedia:ClientSecret, GoogleMedia:RedirectUri.</p></div>}
      <button disabled={!q.data.configured || !!busy} onClick={async () => { setBusy('connect');setError(undefined);try { const result = await api('/google-media/connect','POST',{});window.location.assign(result.url); } catch(e) {setError(e);setBusy('');} }}><ExternalLink size={16}/>Connect Google account</button>
      <p className="activity-storage-footnote">Google Photos cannot be used to host website media and no longer offers API album sharing. Drive provides the activity folders for this integration.</p>
    </section><section className="google-storage-accounts"><h2>Connected accounts</h2>{!q.data.accounts.length && <p>No Google accounts connected yet.</p>}{q.data.accounts.map((c: Row) => <article className="panel" key={c.id}><div><strong>{c.email}</strong><span className="activity-audience">{!c.connected ? 'Disconnected' : c.activeForUploads ? 'Default for new activity folders' : 'Connected'}</span></div><p>Existing activity folders remain linked to the account that created them.</p>{c.connected && <div className="header-actions">{!c.activeForUploads && <button className="secondary" disabled={!!busy} onClick={() => action('/google-media/accounts/' + c.id + '/activate')}>Use for new activities</button>}<button className="secondary" disabled={!!busy} onClick={() => setConfirm(c.id)}><Unplug size={14}/>Disconnect</button></div>}{confirm === c.id && <div className="activity-config"><p>Disconnecting revokes Google access. Media from this account will stop displaying until it is reconnected. Google files are retained.</p><button disabled={!!busy} onClick={() => action('/google-media/accounts/' + c.id + '/disconnect')}>Confirm disconnect</button><button className="secondary" onClick={() => setConfirm('')}>Cancel</button></div>}</article>)}</section></div>}
  </>;
}
