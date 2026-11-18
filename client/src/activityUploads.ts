import {api} from './api';

export type MediaUpload = { key: string; file: File; progress: number; status: string; error?: string; session?: { id: string; uploadUrl: string; chunkSize: number } };

// A resumable session is scoped to one file; Google access/refresh tokens never reach the browser.
export async function uploadActivityMedia(postId: string, task: MediaUpload, update: (patch: Partial<MediaUpload>) => void) {
  const session: NonNullable<MediaUpload['session']> = task.session || await api('/community/posts/' + postId + '/media/start', 'POST', { fileName: task.file.name, mimeType: task.file.type, size: task.file.size });
  update({ session, status: 'Uploading', error: undefined });
  try {
  let offset = 0;
  if (task.session) {
    const status = await fetch(session.uploadUrl, { method: 'PUT', headers: { 'Content-Range': `bytes */${task.file.size}` } });
    if (status.ok) offset = task.file.size;
    else if (status.status === 308) offset = Number(status.headers.get('Range')?.split('-')[1] ?? -1) + 1;
    else throw new Error('The Google upload session expired. Remove this file and select it again.');
  }
  while (offset < task.file.size) {
    const end = Math.min(offset + session.chunkSize, task.file.size);
    const response = await fetch(session.uploadUrl, { method: 'PUT', headers: { 'Content-Type': task.file.type, 'Content-Range': `bytes ${offset}-${end - 1}/${task.file.size}` }, body: task.file.slice(offset, end) });
    if (!response.ok && response.status !== 308) throw new Error('Google upload interrupted. Retry to continue this file.');
    const nextOffset = response.status === 308 ? Number(response.headers.get('Range')?.split('-')[1] ?? -1) + 1 : task.file.size;
    if (nextOffset <= offset || nextOffset > task.file.size) throw new Error('Google could not confirm this chunk. Retry to resume the file.');
    offset = nextOffset;
    update({ progress: Math.round(100 * offset / task.file.size) });
  }
  await api('/community/media/' + session.id + '/complete', 'POST', {});
  update({ progress: 100, status: 'Uploaded', session: undefined });
  } catch (error) { try { await api('/community/media/'+session.id+'/failure','POST',{}); } catch {} throw error; }
}

