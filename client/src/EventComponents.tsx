import {OrgField,PersonField} from './GovernanceUI';
export function EventOrgField(props:any){return <OrgField required={false} {...props} endpoint="/event-organizations"/>}
export function EventPersonField(props:any){return <PersonField required={false} {...props} endpoint="/event-people"/>}
