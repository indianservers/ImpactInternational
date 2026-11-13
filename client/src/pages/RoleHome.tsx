import { lazy, Suspense } from "react";
import { Link, Navigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { api } from "../api";
import { ErrorBox, Skeleton, useWorkspace } from "../App";
import ImpactSetupGuide from "../ImpactSetupGuide";
const Platform = lazy(() => import("./PlatformAdminDashboard"));
const Overview = lazy(() => import("./Dashboard"));
const Member = lazy(() => import("./MemberPortal"));
const Governance = lazy(() => import("./GovernanceClubs").then(m => ({ default: m.GovernanceDashboard })));
export default function RoleHome() {
  const { w } = useWorkspace();
  const q = useQuery<any>({queryKey:["dashboard-route",w?.id],queryFn:()=>api("/dashboard?view=route"),enabled:!!w});
  if(q.isPending)return <Skeleton/>;
  if(q.error)return <ErrorBox error={q.error}/>;
  const d=q.data;
  if(!d || !w)return null;
  if(d.kind==="platform")return <Suspense fallback={<Skeleton/>}><ImpactSetupGuide/><Platform/></Suspense>;
  if(d.kind==="finance")return <Navigate to="/app/finance" replace/>;
  if(d.kind==="committee")return <><section className="panel"><div className="eyebrow">YOUR AUTHORIZED WORKSPACES</div><h1>{d.title}</h1><p>Current committee memberships and responsibilities.</p></section><div className="gov-club-cards">{d.committees.map((c:any)=><Link className="panel" key={c.id} to={"/app/committees/"+c.id}><h2>{c.name}</h2><p>{c.canManage?"Committee leadership workspace":"Committee member workspace"}</p></Link>)}</div><Link to="/app/my-membership">My membership →</Link></>;
  if(d.kind==="member")return <Suspense fallback={<Skeleton/>}><Member/></Suspense>;
  return <Suspense fallback={<Skeleton/>}><section className="role-home-context"><b>{d.title}</b><span>{d.scope.name} · {d.role}</span><small>All information below is limited to your active role and assigned branch.</small></section>{w.permissions.includes("governance.reports")?<Governance/>:<Overview/>}</Suspense>;
}
