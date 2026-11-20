import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { api } from "./api";
import "./i18n.css";

export const languages = [{ code: "en", name: "English" }, { code: "te", name: "తెలుగు" }] as const;
type Language = "en" | "te";
const telugu: Record<string, string> = {
  "Engagement": "భాగస్వామ్యం", "Records": "రికార్డులు", "Privacy": "గోప్యత", "Complete profile": "పూర్తి ప్రొఫైల్", "Personal details": "వ్యక్తిగత వివరాలు",
  "Contact information": "సంప్రదింపు వివరాలు", "Business profile": "వ్యాపార వివరాలు", "Professional": "వృత్తి వివరాలు", "Membership history": "సభ్యత్వ చరిత్ర",
  "Organizational hierarchy": "సంస్థ నిర్మాణం", "Dues & payments": "బకాయిలు, చెల్లింపులు", "Participation": "కార్యక్రమాల్లో భాగస్వామ్యం", "Communication": "సంప్రదింపులు", "Audit history": "మార్పుల చరిత్ర", "Privacy & preferences": "గోప్యత, ప్రాధాన్యతలు",
  "Full legal name": "పూర్తి అధికారిక పేరు", "Preferred display name": "ప్రదర్శించాల్సిన పేరు", "Languages spoken": "మాట్లాడే భాషలు",
  "Select": "ఎంచుకోండి", "PEOPLE & MEMBERSHIP": "సభ్యులు, సభ్యత్వం", "Add a Community Member": "కొత్త సభ్యుడిని చేర్చండి",
  "Create a member profile and connect them to the right community.": "సభ్యుని వివరాలు నమోదు చేసి సరైన క్లబ్‌తో అనుసంధానించండి.",
  "Close registration": "నమోదు మూసివేయండి", "New Member": "కొత్త సభ్యుడు", "A new connection.": "కొత్త పరిచయం.", "A greater impact.": "మరింత ప్రభావం.",
  "One community.": "ఒకే సమాజం.", "Limitless possibilities.": "అపార అవకాశాలు.", "Training · Business": "శిక్షణ · వ్యాపారం", "Service · Leadership": "సేవ · నాయకత్వం",
  "Registration steps": "నమోదు దశలు", "STEP": "దశ", "Registration progress": "నమోదు పురోగతి", "Saved draft ·": "భద్రపరచిన ముసాయిదా ·",
  "Resume draft": "ముసాయిదా కొనసాగించండి", "Discard saved draft": "ముసాయిదా తొలగించండి", "Member Information": "సభ్యుని సమాచారం", "Member ID": "సభ్యుని ఐడీ",
  "Start with their name. Other personal details can be added later.": "ముందుగా పేరు ఇవ్వండి. ఇతర వ్యక్తిగత వివరాలు తరువాత చేర్చవచ్చు.",
  "Member photo": "సభ్యుని ఫొటో", "PNG or JPEG, up to 2 MB": "PNG లేదా JPEG, గరిష్ఠంగా 2 MB", "Recommended: 400 × 400 px": "సూచించిన పరిమాణం: 400 × 400 పిక్సెల్‌లు",
  "Remove photo": "ఫొటో తొలగించండి", "Choose each level in order. Only your authorized community is available.": "ప్రతి స్థాయిని వరుసగా ఎంచుకోండి. మీకు అనుమతి ఉన్న విభాగాలే కనిపిస్తాయి.",
  "Membership type": "సభ్యత్వ రకం", "Current office": "ప్రస్తుత పదవి", "Officer appointments are managed through Leadership & Appointments after registration.": "నమోదు తరువాత నాయకత్వం, నియామకాల విభాగంలో పదవులను నిర్వహించవచ్చు.",
  "Help the community stay connected. Contact details are optional.": "సభ్యులు సంప్రదింపులో ఉండేందుకు వివరాలు ఇవ్వండి. ఇవి తప్పనిసరి కాదు.",
  "Addresses": "చిరునామాలు", "Up to 5 addresses · choose one primary": "గరిష్ఠంగా 5 చిరునామాలు · ఒక ప్రధాన చిరునామా ఎంచుకోండి", "Primary address": "ప్రధాన చిరునామా",
  "+ Add another address": "+ మరో చిరునామా చేర్చండి", "Complete their story. Everything in this section is optional.": "మరిన్ని వివరాలు చేర్చండి. ఈ విభాగం మొత్తం ఐచ్ఛికం.",
  "Emergency contact (name & phone)": "అత్యవసర సంప్రదింపు (పేరు, ఫోన్)", "Social profiles": "సామాజిక మాధ్యమ ప్రొఫైల్‌లు", "Optional handles or HTTPS profile links": "ఐచ్ఛిక యూజర్ పేర్లు లేదా HTTPS ప్రొఫైల్ లింకులు",
  "Notes & additional information": "గమనికలు, అదనపు వివరాలు", "Additional documents": "అదనపు పత్రాలు", "PDF, PNG or JPEG · up to 5 MB each": "PDF, PNG లేదా JPEG · ఒక్కో ఫైల్ గరిష్ఠంగా 5 MB",
  "Choose supporting documents": "అనుబంధ పత్రాలు ఎంచుకోండి", "Up to 5 files. Uploaded securely after registration.": "గరిష్ఠంగా 5 ఫైళ్లు. నమోదు తరువాత భద్రంగా అప్‌లోడ్ అవుతాయి.",
  "Document uploads require member document permissions.": "పత్రాల అప్‌లోడ్‌కు సభ్యుల పత్రాల నిర్వహణ అనుమతి అవసరం.", "Review Registration": "నమోదు వివరాలు పరిశీలించండి", "READY TO CONNECT": "అనుసంధానానికి సిద్ధం",
  "Check the details before adding this member to Impact International.": "సభ్యుని చేర్చే ముందు వివరాలు పరిశీలించండి.", "Member ID is generated when saved": "భద్రపరిచేటప్పుడు సభ్యుని ఐడీ కేటాయించబడుతుంది",
  "Contact": "సంప్రదింపు", "Documents": "పత్రాలు", "Review possible matches": "సరిపోలే వివరాలు పరిశీలించండి", "Shared contact details can belong to a family. Choose carefully.": "ఒకే సంప్రదింపు వివరాలు కుటుంబ సభ్యులకు ఉండవచ్చు. జాగ్రత్తగా ఎంచుకోండి.",
  "Use existing person:": "ఇప్పటికే ఉన్న వ్యక్తిని ఎంచుకోండి:", "· Existing profile and photo will be retained.": "· ఇప్పటికే ఉన్న ప్రొఫైల్, ఫొటో ఉంచబడతాయి.",
  "I reviewed the matches — create a separate person": "సరిపోలికలు పరిశీలించాను — కొత్త వ్యక్తిని సృష్టించండి", "Member": "సభ్యుడు", "is saved. Only remaining document uploads will be retried.": "భద్రపరచబడ్డారు. మిగిలిన పత్రాలు మాత్రమే మళ్లీ అప్‌లోడ్ అవుతాయి.",
  "Finish without remaining documents": "మిగిలిన పత్రాలు లేకుండానే పూర్తి చేయండి", "* Required fields": "* తప్పనిసరి వివరాలు", "Save as Draft": "ముసాయిదాగా భద్రపరచండి",
  "Discard entered information?": "ఇచ్చిన వివరాలు తొలగించాలా?", "Your unsaved changes will be lost. Save a draft to continue on this device later.": "భద్రపరచని మార్పులు పోతాయి. తరువాత కొనసాగించడానికి ముసాయిదా భద్రపరచండి.",
  "Keep editing": "సవరణ కొనసాగించండి", "Discard changes": "మార్పులు తొలగించండి", "President": "అధ్యక్షుడు", "Secretary": "కార్యదర్శి", "Treasurer": "కోశాధికారి",
  "Next step": "తరువాతి దశ", "Review registration": "నమోదు పరిశీలించండి", "Register member": "సభ్యుని నమోదు చేయండి", "Add another member": "మరో సభ్యుడిని చేర్చండి",
  "Founder, Impact International": "వ్యవస్థాపకులు, ఇంపాక్ట్ ఇంటర్నేషనల్", "regions": "ప్రాంతాలు", "clubs": "క్లబ్‌లు", "Find a club": "క్లబ్‌ను వెతకండి",
  "Search club name, code or city": "క్లబ్ పేరు, కోడ్ లేదా నగరం వెతకండి", "No available clubs match your search. Try another name or region.": "మీ శోధనకు క్లబ్‌లు దొరకలేదు. మరో పేరు లేదా ప్రాంతం ప్రయత్నించండి.",
  "Previous clubs": "మునుపటి క్లబ్‌లు", "Page": "పేజీ", "of": "/", "More clubs": "మరిన్ని క్లబ్‌లు", "membership · Pending until approved": "సభ్యత్వం · ఆమోదం అవసరం",
  "Signup is currently unavailable. Please contact the association.": "ప్రస్తుతం నమోదు అందుబాటులో లేదు. సంస్థను సంప్రదించండి.", "Email or phone number": "ఇమెయిల్ లేదా ఫోన్ నంబరు",
  "Email or phone with country code": "ఇమెయిల్ లేదా దేశ కోడ్‌తో ఫోన్ నంబరు", "One-time code": "ఒకసారి ఉపయోగించే కోడ్", "6-digit code": "6 అంకెల కోడ్", "Codes expire in 10 minutes.": "కోడ్ 10 నిమిషాల వరకు చెల్లుతుంది.",
  "Membership ID, email, or phone": "సభ్యత్వ ఐడీ, ఇమెయిల్ లేదా ఫోన్",
  "Membership ID or email": "సభ్యత్వ ఐడీ లేదా ఇమెయిల్",
  "Change sign-in details": "సైన్ ఇన్ వివరాలను మార్చండి",
  "e.g. ICI-0132": "ఉదా. ICI-0132",
  "Use your membership ID if you share a phone number or email. You need an email address only you use to receive your code.": "ఫోన్ నంబరు లేదా ఇమెయిల్‌ను ఇతరులతో పంచుకుంటే మీ సభ్యత్వ ఐడీని వాడండి. కోడ్ అందుకోవడానికి మీరు మాత్రమే ఉపయోగించే ఇమెయిల్ చిరునామా అవసరం.",
  "Members sign in with a one-time email code.": "సభ్యులు ఇమెయిల్‌కు వచ్చే ఒక్కసారి ఉపయోగించే కోడ్‌తో సైన్ ఇన్ చేస్తారు.",
  "If your sign-in details match an eligible account, a code will be sent to its registered email.": "మీ సైన్ ఇన్ వివరాలు అర్హత గల ఖాతాకు సరిపోతే, నమోదైన ఇమెయిల్‌కు కోడ్ పంపబడుతుంది.",
  "Change email or phone": "ఇమెయిల్ లేదా ఫోన్ మార్చండి", "Resend code": "కోడ్ మళ్లీ పంపండి", "Sign in after verifying your email": "ఇమెయిల్ ధృవీకరించిన తరువాత ప్రవేశించండి",
  "I agree to the development terms and privacy notice below.": "కింద ఇచ్చిన అభివృద్ధి దశ నిబంధనలు, గోప్యతా ప్రకటనకు అంగీకరిస్తున్నాను.",
  "Change password": "పాస్‌వర్డ్ మార్చండి", "Current password": "ప్రస్తుత పాస్‌వర్డ్", "Save password": "పాస్‌వర్డ్ భద్రపరచండి",
  "Language": "భాష", "Home": "ముఖ్య పేజీ", "Dashboard": "డాష్‌బోర్డ్", "Overview": "అవలోకనం",
  "People": "సభ్యులు", "Members": "సభ్యులు", "All members": "సభ్యులందరూ", "Directory": "సభ్యుల పరిచయాలు",
  "Membership": "సభ్యత్వం", "Networking": "పరిచయ వేదిక", "Leadership": "నాయకత్వం", "Organization": "సంస్థ",
  "Activities": "కార్యక్రమాలు", "ID cards": "గుర్తింపు కార్డులు", "Finance": "ఆర్థిక వ్యవహారాలు", "Intelligence": "విశ్లేషణలు",
  "Settings & access": "సెట్టింగులు, అనుమతులు", "Settings": "సెట్టింగులు", "More": "మరిన్ని", "View": "చూడండి",
  "YOUR ORGANIZATION": "మీ సంస్థ", "ORGANIZATION": "సంస్థ", "Profile": "ప్రొఫైల్", "Password": "పాస్‌వర్డ్",
  "Sessions": "లాగిన్ సెషన్లు", "Certifications": "ధృవపత్రాలు", "Your profile": "మీ ప్రొఫైల్", "Save profile": "ప్రొఫైల్ భద్రపరచండి",
  "Content language preference": "భాష ఎంపిక", "Full name": "పూర్తి పేరు", "Email": "ఇమెయిల్", "Phone": "ఫోన్",
  "Personal time zone": "మీ సమయ మండలం", "Theme": "రంగుల విధానం", "Light": "లేత రంగులు", "Dark": "ముదురు రంగులు",
  "Sign out": "నిష్క్రమించండి", "Close navigation": "నావిగేషన్ మూసివేయండి", "Close navigation menu": "మెనూ మూసివేయండి",
  "Open navigation": "మెనూ తెరవండి", "My Impact profile & security": "నా ఇంపాక్ట్ ప్రొఫైల్, భద్రత",
  "Member portal": "సభ్యుల పోర్టల్", "Welcome back": "మళ్లీ స్వాగతం", "Sign in to your Impact International account.": "మీ ఇంపాక్ట్ ఇంటర్నేషనల్ ఖాతాలో ప్రవేశించండి.",
  "Continue with Google": "Googleతో కొనసాగించండి", "or sign in with email": "లేదా ఇమెయిల్‌తో ప్రవేశించండి",
  "Email address": "ఇమెయిల్ చిరునామా", "Forgot password?": "పాస్‌వర్డ్ మర్చిపోయారా?", "Sign in": "ప్రవేశించండి",
  "Show password": "పాస్‌వర్డ్ చూపించండి", "Hide password": "పాస్‌వర్డ్ దాచండి", "Create an account": "ఖాతా తెరవండి",
  "New to Impact International?": "ఇంపాక్ట్ ఇంటర్నేషనల్‌కు కొత్తవారా?", "About Impact International": "ఇంపాక్ట్ ఇంటర్నేషనల్ గురించి",
  "Learn. Connect.": "నేర్చుకోండి. కలవండి.", "Lead.": "నడిపించండి.", "Training · Business · Service · Leadership": "శిక్షణ · వ్యాపారం · సేవ · నాయకత్వం",
  "Join the community": "మా సమాజంలో చేరండి", "Create your account": "మీ ఖాతా తెరవండి", "Account recovery": "ఖాతా పునరుద్ధరణ",
  "Email verification": "ఇమెయిల్ ధృవీకరణ", "Verify your email": "మీ ఇమెయిల్ ధృవీకరించండి", "Account security": "ఖాతా భద్రత",
  "Secure your account": "మీ ఖాతాను భద్రపరచండి", "Send reset link": "పాస్‌వర్డ్ మార్పు లింక్ పంపండి",
  "Enter your registered email and we’ll send a link to reset your password.": "మీ నమోదిత ఇమెయిల్ ఇవ్వండి. పాస్‌వర్డ్ మార్చడానికి లింక్ పంపుతాము.",
  "Enter your registered email and we’ll send a new verification link.": "మీ నమోదిత ఇమెయిల్ ఇవ్వండి. కొత్త ధృవీకరణ లింక్ పంపుతాము.",
  "Resend verification email": "ధృవీకరణ ఇమెయిల్ మళ్లీ పంపండి", "Remember your password?": "పాస్‌వర్డ్ గుర్తుందా?", "Ready to sign in?": "ప్రవేశించడానికి సిద్ధమా?",
  "Choose your community and introduce yourself. Your club or region president will review your application.": "మీ ప్రాంతం, క్లబ్‌ను ఎంచుకుని వివరాలు ఇవ్వండి. మీ క్లబ్ లేదా ప్రాంత అధ్యక్షులు దరఖాస్తును పరిశీలిస్తారు.",
  "Join workshops and certifications. No club membership is required.": "వర్క్‌షాపులు, ధృవపత్ర కార్యక్రమాల్లో చేరండి. క్లబ్ సభ్యత్వం అవసరం లేదు.",
  "How would you like to join?": "మీరు ఎలా చేరాలనుకుంటున్నారు?", "Apply for club membership": "క్లబ్ సభ్యత్వానికి దరఖాస్తు చేయండి",
  "Learning & certifications — no club required": "శిక్షణ, ధృవపత్రాలు — క్లబ్ అవసరం లేదు", "Choose your community": "మీ ప్రాంతం, క్లబ్‌ను ఎంచుకోండి",
  "National": "జాతీయ విభాగం", "Region": "ప్రాంతం", "Club": "క్లబ్", "International": "అంతర్జాతీయ విభాగం",
  "Choose an available national": "జాతీయ విభాగాన్ని ఎంచుకోండి", "Choose an available region": "ప్రాంతాన్ని ఎంచుకోండి", "Choose a club in your region": "మీ ప్రాంతంలోని క్లబ్‌ను ఎంచుకోండి",
  "Country code": "దేశ కోడ్", "Mobile number": "మొబైల్ నంబరు", "Confirm password": "పాస్‌వర్డ్ నిర్ధారించండి",
  "At least 12 characters.": "కనీసం 12 అక్షరాలు ఉండాలి.", "Terms & privacy notice": "నిబంధనలు, గోప్యతా ప్రకటన",
  "Already part of Impact International?": "ఇప్పటికే ఇంపాక్ట్ ఇంటర్నేషనల్ సభ్యులా?", "Please wait…": "దయచేసి వేచి ఉండండి…",
  "Sign up — pending approval": "నమోదు చేయండి — ఆమోదం అవసరం", "Create learning portal account": "శిక్షణ పోర్టల్ ఖాతా తెరవండి",
  "Pending approval": "ఆమోదం కోసం వేచి ఉంది", "Complete request": "అభ్యర్థన పూర్తి చేయండి", "New password": "కొత్త పాస్‌వర్డ్",
  "Request failed": "అభ్యర్థన విఫలమైంది", "Return to sign in": "లాగిన్‌కు తిరిగి వెళ్లండి",
  "Basic Details": "ప్రాథమిక వివరాలు", "Community & Membership": "ప్రాంతం, సభ్యత్వం", "Address & Contact": "చిరునామా, సంప్రదింపు వివరాలు",
  "Additional Information": "అదనపు సమాచారం", "Membership category": "సభ్యత్వ వర్గం", "Regular": "సాధారణ సభ్యత్వం", "Choose category": "వర్గాన్ని ఎంచుకోండి",
  "Add member": "సభ్యుడిని చేర్చండి", "Add Member": "సభ్యుడిని చేర్చండి", "Next": "తరువాత", "Back": "వెనుకకు", "Cancel": "రద్దు చేయండి", "Save": "భద్రపరచండి",
  "Legal name": "అధికారిక పేరు", "Display name": "ప్రదర్శన పేరు", "Date of birth": "పుట్టిన తేదీ", "Gender": "లింగం", "Male": "పురుషుడు", "Female": "మహిళ", "Other": "ఇతర",
  "Marital status": "వైవాహిక స్థితి", "Single": "అవివాహితులు", "Married": "వివాహితులు", "Spouse name": "జీవిత భాగస్వామి పేరు", "Anniversary": "వివాహ వార్షికోత్సవం",
  "Languages": "భాషలు", "Skills": "నైపుణ్యాలు", "Profession": "వృత్తి", "Company": "సంస్థ పేరు", "Designation": "హోదా", "Country": "దేశం", "State": "రాష్ట్రం", "City": "నగరం",
  "Postal code": "పిన్ కోడ్", "Address": "చిరునామా", "Joining date": "చేరిన తేదీ", "Membership status": "సభ్యత్వ స్థితి", "Active": "క్రియాశీలం",
  "Past office bearers": "గత పదాధికారులు", "Past presidents": "గత అధ్యక్షులు", "All past offices": "గత పదవులన్నీ",
  "Service history": "పదవీ సేవా చరిత్ర", "Add historical record": "గత సేవా వివరాలు చేర్చండి", "Edit historical record": "గత సేవా వివరాలు సవరించండి",
  "Office bearer name": "పదాధికారి పేరు", "Office title": "పదవి", "Start year": "ప్రారంభ సంవత్సరం", "End year": "ముగింపు సంవత్సరం", "Service years": "సేవ చేసిన సంవత్సరాలు",
  "Include in past presidents": "గత అధ్యక్షుల జాబితాలో చేర్చండి", "Notes": "గమనికలు", "Save record": "వివరాలు భద్రపరచండి", "Saving…": "భద్రపరుస్తోంది…",
  "Edit": "సవరించండి", "Archive": "ఆర్కైవ్ చేయండి", "Archive reason": "ఆర్కైవ్ చేయడానికి కారణం", "Archive record": "వివరాలు ఆర్కైవ్ చేయండి",
  "All years": "అన్ని సంవత్సరాలు", "Service year": "సేవా సంవత్సరం", "Previous": "మునుపటి", "Historical record": "గత సేవా నమోదు", "Appointment": "నియామకం",
  "No past office bearers recorded for this selection.": "ఈ ఎంపికకు గత పదాధికారుల వివరాలు లేవు.",
  "Completed appointments appear automatically. Add older service records here; these records do not grant access or membership.": "పూర్తయిన నియామకాలు స్వయంగా కనిపిస్తాయి. పాత సేవా వివరాలు ఇక్కడ చేర్చండి. వీటి ద్వారా ప్రవేశ అనుమతులు లేదా సభ్యత్వం లభించవు.",
  "The organization and service years stay attached to this historical record.": "ఈ సంస్థ, సేవ చేసిన సంవత్సరాలు ఈ చరిత్ర నమోదుతో అనుసంధానమై ఉంటాయి.",
  "Historical record saved.": "గత సేవా వివరాలు భద్రపరచబడ్డాయి.", "Historical record archived.": "గత సేవా వివరాలు ఆర్కైవ్ చేయబడ్డాయి.",
  "Organization Structure & Governance": "సంస్థ నిర్మాణం, పరిపాలన", "Children": "ఉప విభాగాలు", "History": "చరిత్ర", "Decisions": "నిర్ణయాలు",
  "Applications": "దరఖాస్తులు", "Requests": "అభ్యర్థనలు", "Configuration": "అమరికలు", "Tools": "సాధనాలు", "Reports": "నివేదికలు", "Governance": "పరిపాలన",
  "Organizations": "సంస్థల విభాగాలు", "Regions": "ప్రాంతాలు", "Clubs": "క్లబ్‌లు", "Committees": "కమిటీలు", "Events": "కార్యక్రమాలు", "My membership": "నా సభ్యత్వం",
};

const Context = createContext({ language: "en" as Language, setLanguage: (_: string) => {}, t: (text: string) => text });
function initialLanguage(): Language {
  return document.cookie.split("; ").find(x => x.startsWith("impact_language="))?.split("=")[1] === "te" ? "te" : "en";
}
export function LanguageProvider({ children }: { children: ReactNode }) {
  const [language, update] = useState<Language>(initialLanguage);
  const setLanguage = useCallback((value: string) => update(value === "te" ? "te" : "en"), []);
  useEffect(() => {
    document.documentElement.lang = language;
    document.cookie = `impact_language=${language}; Path=/; Max-Age=31536000; SameSite=Lax${location.protocol === "https:" ? "; Secure" : ""}`;
  }, [language]);
  const t = useCallback((text: string) => language === "te" ? telugu[text] || text : text, [language]);
  return <Context.Provider value={useMemo(() => ({ language, setLanguage, t }), [language, setLanguage, t])}>{children}</Context.Provider>;
}
export const useLanguage = () => useContext(Context);
export function LanguagePicker({ persist = false }: { persist?: boolean }) {
  const { language, setLanguage, t } = useLanguage(), qc = useQueryClient();
  const [busy, setBusy] = useState(false), [error, setError] = useState("");
  return <label className="language-picker"><span>{t("Language")}</span><select aria-label="Language / భాష" value={language} disabled={busy} onChange={async e => {
    const locale = e.target.value; setBusy(true); setError("");
    try {
      if (persist) { await api("/users/language", "PUT", { locale }); qc.setQueryData(["me"], (previous: any) => previous ? { ...previous, locale } : previous); }
      setLanguage(locale);
    } catch { setError("Could not save language. Please try again."); }
    finally { setBusy(false); }
  }}>{languages.map(x => <option value={x.code} key={x.code}>{x.name}</option>)}</select>{error && <small role="alert">{error}</small>}</label>;
}
