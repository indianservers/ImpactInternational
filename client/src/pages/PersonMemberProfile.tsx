import {useParams} from 'react-router-dom';
import {useQuery} from '@tanstack/react-query';
import {api} from '../api';
import {useWorkspace,Skeleton,ErrorBox} from '../App';
import MemberProfile from './MemberProfile';
export default function PersonMemberProfile(){const {personId}=useParams(),{w}=useWorkspace();const q=useQuery({queryKey:['person-member-profile',w?.id,personId],queryFn:()=>api('/members/by-person/'+personId),enabled:!!w&&!!personId});if(q.isPending)return <Skeleton/>;if(q.error)return <ErrorBox error={q.error}/>;return <MemberProfile key={q.data.membershipId} membershipId={q.data.membershipId} directoryMode={q.data.directory}/>}
