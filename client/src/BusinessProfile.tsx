import { useId, useState } from 'react';
import { BriefcaseBusiness, Building2, Tags, Globe, MapPin, X } from 'lucide-react';
import './BusinessProfile.css';

export const businessCategories: Record<string, string[]> = {
  'Technology': ['Software development', 'IT consulting', 'Cybersecurity', 'Digital services'],
  'Professional services': ['Consulting', 'Legal services', 'Accounting', 'Marketing & advertising'],
  'Retail & commerce': ['Retail', 'Wholesale', 'E-commerce', 'Import & export'],
  'Manufacturing': ['Industrial products', 'Food processing', 'Textiles', 'Engineering'],
  'Health & wellness': ['Healthcare', 'Fitness', 'Wellness services', 'Medical supplies'],
  'Real estate & construction': ['Real estate', 'Construction', 'Architecture', 'Interior design'],
  'Education & training': ['Education', 'Professional training', 'Coaching', 'Learning technology'],
  'Finance & insurance': ['Financial services', 'Insurance', 'Banking', 'Investment services'],
  'Hospitality & travel': ['Hospitality', 'Travel services', 'Food & beverage', 'Events'],
  'Agriculture': ['Farming', 'Agricultural supplies', 'Agri technology'],
  'Automotive & transport': ['Automotive sales', 'Vehicle services', 'Logistics', 'Freight'],
  'Media & creative': ['Design', 'Photography', 'Publishing', 'Film & video'],
  'Energy & utilities': ['Renewable energy', 'Electrical services', 'Water services'],
  'Nonprofit & social services': ['Community development', 'Charitable services', 'Social enterprise'],
  'Other': [],
};
export function BusinessProfileFields({ value, onChange }: { value: any; onChange: (patch: any) => void }) {
  const id = useId(), [draft, setDraft] = useState('');
  const tags: string[] = value.businessTags || [];
  function addTags() {
    const next = [...tags];
    for (const item of draft.split(',')) {
      const tag = item.trim().replace(/\s+/g, ' ');
      if (tag && tag.length <= 100 && !next.some(t => t.toLowerCase() === tag.toLowerCase()) && next.length < 30) next.push(tag);
    }
    onChange({ businessTags: next }); setDraft('');
  }
  return <section className="business-editor" aria-label="Business profile details">
    <div className="business-section-title"><BriefcaseBusiness size={20}/><div><h3>Business profile</h3><p>Help members understand what you do and where you can collaborate.</p></div></div>
    <div className="form-grid">
      <label>Business category<input maxLength={200} list={id+'-categories'} value={value.businessCategory || ''} onChange={e => onChange({ businessCategory: e.target.value, businessSubcategory: '' })} placeholder="Choose or enter a category"/><datalist id={id+'-categories'}>{Object.keys(businessCategories).map(c => <option key={c} value={c}/>)}</datalist></label>
      <label>Business subcategory<input maxLength={200} list={id+'-subcategories'} value={value.businessSubcategory || ''} onChange={e => onChange({ businessSubcategory: e.target.value })} placeholder="Your business specialty"/><datalist id={id+'-subcategories'}>{(businessCategories[value.businessCategory] || []).map(c => <option key={c} value={c}/>)}</datalist></label>
    </div>
    <label>About your business<textarea rows={4} maxLength={2000} value={value.businessDescription || ''} onChange={e => onChange({ businessDescription: e.target.value })} placeholder="Describe your products, services, customers, and what makes your business distinctive."/><small>{(value.businessDescription || '').length}/2,000 characters</small></label>
    <label htmlFor={id+'-tags'}>Business tags</label><div className="business-tag-input"><input id={id+'-tags'} value={draft} maxLength={1000} disabled={tags.length >= 30} onChange={e => setDraft(e.target.value)} onBlur={() => { if(draft.trim()) addTags(); }} onKeyDown={e => { if(e.key === 'Enter' || e.key === ',') {e.preventDefault();addTags();} }} placeholder="e.g. B2B, sustainable products, web design"/><button type="button" className="secondary" disabled={!draft.trim() || tags.length >= 30} onClick={addTags}>Add tags</button></div>
    <small>Press Enter or comma to add a tag. Up to 30 tags, 100 characters each.</small>
    <div className="business-tags">{tags.map(tag => <span key={tag}>{tag}<button type="button" aria-label={'Remove tag '+tag} onClick={() => onChange({ businessTags: tags.filter(t => t !== tag) })}><X size={13}/></button></span>)}</div>
    <p className="business-privacy">Business details follow your professional directory visibility preferences.</p>
  </section>;
}
export function BusinessProfileCard({ professional: p }: { professional: any }) {
  if (!p) return <section className="business-empty"><BriefcaseBusiness size={28}/><h3>Business profile is private</h3><p>This member has not shared professional details with this directory.</p></section>;
  const hasBusiness = p.company || p.businessCategory || p.businessSubcategory || p.businessDescription || p.businessTags?.length;
  if (!hasBusiness) return <section className="business-empty"><Building2 size={28}/><h3>A place for your business</h3><p>Add your company, category and services in your professional profile to introduce your business to the community.</p></section>;
  const website = /^https?:\/\//i.test(p.website || '') ? p.website : null;
  return <section className="business-profile-card" aria-label="Business profile">
    <header><div className="business-monogram"><Building2 size={28}/></div><div><span className="business-eyebrow">MEMBER BUSINESS</span><h2>{p.company || 'Independent professional'}</h2><p>{[p.designation, p.profession].filter(Boolean).join(' · ')}</p></div></header>
    {(p.businessCategory || p.businessSubcategory) && <div className="business-category"><BriefcaseBusiness size={16}/><span>{p.businessCategory}</span>{p.businessSubcategory && <><span aria-hidden="true">/</span><strong>{p.businessSubcategory}</strong></>}</div>}
    {p.businessDescription && <p className="business-description">{p.businessDescription}</p>}
    {!!p.businessTags?.length && <div className="business-tags"><Tags size={16} aria-label="Business tags"/>{p.businessTags.map((tag:string) => <span key={tag}>{tag}</span>)}</div>}
    {(p.workLocation || website) && <footer>{p.workLocation && <span><MapPin size={16}/>{p.workLocation}</span>}{website && <a href={website} target="_blank" rel="noopener noreferrer"><Globe size={16}/>Visit business website ↗</a>}</footer>}
  </section>;
}

