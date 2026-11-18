import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { CalendarDays, MapPin, Users } from 'lucide-react';
import { api } from '../api';
import { ErrorBox, Skeleton } from '../App';
import { ActivityGallery } from '../ActivityMedia';
import './Community.css';

export default function PublicActivities() {
  const { tenantId, postId } = useParams(), [page, setPage] = useState(1);
  const q = useQuery({ queryKey: ['public-activities', tenantId, postId, page], queryFn: () => api('/public/activities/' + tenantId + '?page=' + page + (postId ? '&postId=' + postId : '')) });
  return <main className="public-activities"><header><div><div className="eyebrow">IMPACT INTERNATIONAL · IN ACTION</div><h1>Stories of impact</h1><p>Activities, community service and moments shared publicly by our clubs and leaders.</p></div><Link className="button secondary" to="/">Member sign in</Link></header>{postId && <Link className="activity-public-link" to={'/activities/' + tenantId}>All public activities</Link>}<ErrorBox error={q.error}/>
    {q.isPending ? <Skeleton/> : q.data?.items.length ? q.data.items.map((p: any) => <article className="cm-post" key={p.id} id={'activity-' + p.id}><div className="cm-post-head"><div className="cm-unit-icon">{p.organization[0]}</div><div><strong>{p.organization}</strong><small>Public activity · {new Date(p.createdAt).toLocaleDateString()}</small></div></div><span className="cm-category">{p.category}</span><h2>{p.title}</h2><p className="cm-post-body">{p.body}</p><ActivityGallery media={p.media} publicView/>{p.taggedMembers?.length>0&&<p className="cm-helper">With {p.taggedMembers.map((t:any)=>t.name).join(", ")}</p>}<div className="cm-post-facts"><span><CalendarDays size={14}/>{p.activityOn}</span>{p.location && <span><MapPin size={14}/>{p.location}</span>}{p.participants > 0 && <span><Users size={14}/>{p.participants} participants</span>}</div></article>) : <section className="cm-empty"><h2>More stories coming soon</h2><p>Public activities will appear here when a club or leader shares an update.</p></section>}
    {q.data?.total > 15 && <div className="cm-pager"><button className="secondary" disabled={page === 1} onClick={() => setPage(page - 1)}>Previous</button><span>Page {page}</span><button className="secondary" disabled={page * 15 >= q.data.total} onClick={() => setPage(page + 1)}>Next</button></div>}
  </main>;
}
