import {Link} from 'react-router-dom';
import {useWorkspace} from './App';
export default function MemberPersonLink({personId,children}:{personId?:string|null;children:React.ReactNode}){const {w}=useWorkspace();return personId&&w?.permissions.some(p=>['members.view','directory.view'].includes(p))?<Link className="member-profile-link" to={'/app/people/'+personId}>{children}</Link>:<span>{children}</span>}
