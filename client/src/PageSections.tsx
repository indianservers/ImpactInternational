import {Children, isValidElement, type ReactNode} from 'react';
import {useSearchParams} from 'react-router-dom';
import './PageSections.css';

function text(node:ReactNode):string {
  if(typeof node==='string'||typeof node==='number')return String(node);
  if(isValidElement<{children?:ReactNode}>(node))return text(node.props.children);
  return Children.toArray(node).map(child=>isValidElement<{children?:ReactNode}>(child)?text(child.props.children):typeof child==='string'?child:'').join('');
}
function title(node:ReactNode):string {
  if(!isValidElement<{title?:string;children?:ReactNode}>(node))return '';
  if(node.props.title)return node.props.title;
  if(['h2','h3'].includes(String(node.type)))return text(node.props.children);
  for(const child of Children.toArray(node.props.children)){const value=title(child);if(value)return value;}
  return '';
}

/** URL-addressable sections preserve filters and browser Back/Forward navigation. */
export default function PageSections({id,children,className='',labels,perPage=2}:{id:string;children:ReactNode;className?:string;labels?:string[];perPage?:number}) {
  const [params,setParams]=useSearchParams();
  const items=Children.toArray(children).filter(child=>typeof child!=='string');
  const pages=Array.from({length:Math.ceil(items.length/perPage)},(_,i)=>items.slice(i*perPage,(i+1)*perPage));
  const names=pages.map((page,i)=>labels?.[i]||page.map(title).filter(Boolean).join(' & ')||`Section ${i+1}`);
  const key=`view-${id}`,requested=Number(params.get(key)||0),active=Number.isInteger(requested)&&requested>=0&&requested<pages.length?requested:0;
  if(pages.length<=1)return <div className={className}>{children}</div>;
  return <section className="page-sections" aria-label={id.replaceAll('-',' ')+' subpages'}>
    <nav className="page-sections-nav" aria-label={id.replaceAll('-',' ')+' sections'}>{names.map((name,i)=><button type="button" key={i} aria-current={i===active?'page':undefined} onClick={()=>{setParams(previous=>{const next=new URLSearchParams(previous);if(i)next.set(key,String(i));else next.delete(key);return next;});}}>{name}</button>)}</nav>
    <div className={className}>{pages[active]}</div>
    <div className="page-sections-pager"><span>{names[active]} · {active+1} of {pages.length}</span>{active+1<pages.length&&<button type="button" onClick={()=>setParams(previous=>{const next=new URLSearchParams(previous);next.set(key,String(active+1));return next;})}>Next: {names[active+1]} →</button>}</div>
  </section>;
}
