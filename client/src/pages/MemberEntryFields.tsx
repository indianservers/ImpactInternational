import { useId, useState } from "react";
import { X } from "lucide-react";
import { useLanguage } from "../i18n";

export const memberChoices = {
    profession: ["Accountant", "Architect", "Business owner", "Civil servant", "Consultant", "Doctor", "Educator", "Engineer", "Farmer", "Healthcare professional", "Lawyer", "Marketing professional", "Sales professional", "Software professional", "Student"],
    designation: ["Founder", "Co-founder", "Owner", "Partner", "Managing Director", "Director", "CEO", "General Manager", "Manager", "Team Lead", "Consultant", "Associate", "Executive"],
    industry: ["Agriculture", "Automotive", "Banking", "Construction", "Education", "Energy", "Financial services", "Food & beverage", "Government", "Healthcare", "Hospitality", "Information technology", "Insurance", "Logistics", "Manufacturing", "Media", "Nonprofit", "Professional services", "Real estate", "Retail", "Telecommunications", "Textiles", "Travel & tourism"],
    employmentStatus: ["Employed full-time", "Employed part-time", "Self-employed", "Freelance / Contract", "Business owner", "Seeking employment", "Student", "Retired", "Career break"],
    educationLevel: ["Secondary school", "Higher secondary school", "Vocational training", "Diploma", "Bachelor’s degree", "Master’s degree", "Doctorate", "Professional qualification"],
    qualification: ["BA", "BCom", "BSc", "BBA", "BCA", "BE / BTech", "BEd", "LLB", "MBBS", "MA", "MCom", "MSc", "MBA", "MCA", "ME / MTech", "CA", "CMA", "CS", "PhD"],
    networking: ["Business referrals", "Partnerships", "Mentoring", "Professional development", "Community service", "Social connections", "Open to all"],
    country: ["India", "Australia", "Bangladesh", "Canada", "France", "Germany", "Japan", "Malaysia", "Nepal", "New Zealand", "Oman", "Pakistan", "Qatar", "Saudi Arabia", "Singapore", "South Africa", "Sri Lanka", "United Arab Emirates", "United Kingdom", "United States"],
    indianStates: ["Andaman and Nicobar Islands", "Andhra Pradesh", "Arunachal Pradesh", "Assam", "Bihar", "Chandigarh", "Chhattisgarh", "Dadra and Nagar Haveli and Daman and Diu", "Delhi", "Goa", "Gujarat", "Haryana", "Himachal Pradesh", "Jammu and Kashmir", "Jharkhand", "Karnataka", "Kerala", "Ladakh", "Lakshadweep", "Madhya Pradesh", "Maharashtra", "Manipur", "Meghalaya", "Mizoram", "Nagaland", "Odisha", "Puducherry", "Punjab", "Rajasthan", "Sikkim", "Tamil Nadu", "Telangana", "Tripura", "Uttar Pradesh", "Uttarakhand", "West Bengal"],
    indianCities: ["Ahmedabad", "Bengaluru", "Bhubaneswar", "Chandigarh", "Chennai", "Coimbatore", "Delhi", "Guntur", "Guwahati", "Hyderabad", "Indore", "Jaipur", "Kochi", "Kolkata", "Lucknow", "Mumbai", "Nagpur", "Patna", "Pune", "Secunderabad", "Surat", "Thiruvananthapuram", "Tirupati", "Vadodara", "Vijayawada", "Visakhapatnam", "Warangal"],
};

export const memberTagSuggestions = {
    languages: ["English", "Hindi", "Telugu", "Tamil", "Kannada", "Malayalam", "Marathi", "Gujarati", "Bengali", "Punjabi", "Urdu", "Odia", "Assamese", "Sanskrit", "Arabic", "French", "German", "Spanish"],
    skills: ["Communication", "Leadership", "Public speaking", "Project management", "Fundraising", "Event planning", "Mentoring", "Negotiation", "Research", "Data analysis", "Design", "Software development"],
    expertise: ["Accounting", "Business development", "Education", "Entrepreneurship", "Finance", "Healthcare", "Human resources", "Law", "Marketing", "Operations", "Technology", "Community development"],
    interests: ["Community service", "Volunteering", "Education", "Environment", "Health & wellness", "Sports", "Arts & culture", "Travel", "Reading", "Technology", "Entrepreneurship", "Youth development"],
    businessTags: ["B2B", "B2C", "Consulting", "Local business", "Small business", "Startup", "E-commerce", "Export", "Wholesale", "Retail", "Professional services", "Sustainable products"],
};

type ChoiceProps = {
    label: string;
    value: string;
    choices: readonly string[];
    onChange: (value: string) => void;
};

export function MemberChoice({ label, value, choices, onChange }: ChoiceProps) {
    const { t } = useLanguage();
    const [custom, setCustom] = useState(false);
    const showCustom = custom || (!!value && !choices.includes(value));

    return <div className="aw-field">
        <label>{t(label)}
            <select value={showCustom ? "__custom__" : value} onChange={e => {
                const next = e.target.value;
                setCustom(next === "__custom__");
                onChange(next === "__custom__" ? "" : next);
            }}>
                <option value="">{t("Select")}</option>
                {choices.map(choice => <option key={choice} value={choice}>{t(choice)}</option>)}
                <option value="__custom__">{t("Other (enter manually)")}</option>
            </select>
        </label>
        {showCustom && <label>{t("Custom value")}
            <input aria-label={t(label) + " (" + t("custom") + ")"} maxLength={200} value={value} onChange={e => onChange(e.target.value)}/>
        </label>}
    </div>;
}

export function MemberTags({ label, values, suggestions, onChange }: {
    label: string;
    values: string[];
    suggestions: readonly string[];
    onChange: (values: string[]) => void;
}) {
    const { t } = useLanguage();
    const id = useId();
    const [search, setSearch] = useState("");
    const draft = search.trim().replace(/\s+/g, " ");
    const term = draft.toLowerCase();
    const full = values.length >= 30;
    const hasValue = (value: string) => values.some(tag => tag.toLowerCase() === value.toLowerCase());
    const matching = suggestions.filter(item => !hasValue(item) && item.toLowerCase().includes(term));
    const exact = matching.find(item => item.toLowerCase() === term);
    const canAdd = !full && !!draft && !draft.includes(",") && !hasValue(draft);

    function add(value: string) {
        onChange([...values, value]);
        setSearch("");
    }

    return <div className="aw-field aw-tag-field" role="group" aria-labelledby={id}>
        <span id={id}>{t(label)}</span>
        {values.length > 0 && <ul className="aw-tag-list">
            {values.map((value, index) => <li key={index} className="aw-tag">
                {value}
                <button type="button" aria-label={t("Remove") + " " + value} onClick={() => onChange(values.filter((_, i) => i !== index))}><X size={14}/></button>
            </li>)}
        </ul>}
        {full ? <p role="status">{t("Maximum of 30 selections reached.")}</p> : <details>
            <summary tabIndex={0}>{t("Choose or add")} {t(label).toLowerCase()}</summary>
            <label>
                <input aria-label={t("Search or add") + " " + t(label).toLowerCase()} placeholder={t("Search or add a custom value")} maxLength={100} value={search} onChange={e => setSearch(e.target.value)} onKeyDown={e => {
                    if (e.key !== "Enter" || e.nativeEvent.isComposing) return;
                    e.preventDefault();
                    if (canAdd) add(exact || draft);
                }}/>
            </label>
            <div className="aw-tag-options">
                {matching.map(item => <button key={item} type="button" onClick={() => add(item)}>{t(item)}</button>)}
                {canAdd && !exact && <button type="button" onClick={() => add(draft)}>{t("Add")} “{draft}”</button>}
                {!matching.length && !canAdd && <span>{t("No available matches")}</span>}
            </div>
            {draft.includes(",") && <small>{t("Add one value at a time.")}</small>}
        </details>}
    </div>;
}
