import { initializeApp } from "@firebase/app";
import { browserPopupRedirectResolver, GoogleAuthProvider, initializeAuth, inMemoryPersistence, signInWithPopup, signOut } from "@firebase/auth";

const app = initializeApp({
  apiKey: "AIzaSyBTQn79BLMQ969V-jZiTaERxle4CzGsvq8",
  authDomain: "impact-international-22932.firebaseapp.com",
  projectId: "impact-international-22932",
  storageBucket: "impact-international-22932.firebasestorage.app",
  messagingSenderId: "362670266675",
  appId: "1:362670266675:web:82def6330f37910f614646",
});
const auth = initializeAuth(app, { persistence: inMemoryPersistence, popupRedirectResolver: browserPopupRedirectResolver });
const provider = new GoogleAuthProvider();
provider.setCustomParameters({ prompt: "select_account" });

export async function googleIdToken() {
  try {
    const result = await signInWithPopup(auth, provider);
    return await result.user.getIdToken(true);
  } finally {
    await signOut(auth);
  }
}

export function googleSignInError(error: unknown): Error {
  const code = (error as { code?: string })?.code;
  const messages: Record<string, string> = {
    "auth/popup-closed-by-user": "Google sign-in was cancelled. Please try again.",
    "auth/cancelled-popup-request": "Google sign-in was cancelled. Please try again.",
    "auth/popup-blocked": "Allow pop-ups for this website, then try Google sign-in again.",
    "auth/unauthorized-domain": "Google sign-in is not enabled for this website. Please contact your administrator.",
    "auth/operation-not-allowed": "Google sign-in is not enabled yet. Please contact your administrator.",
    "auth/network-request-failed": "Could not reach Google. Check your connection and try again.",
    "auth/account-exists-with-different-credential": "This email uses another sign-in method. Sign in with that method first.",
  };
  return code ? new Error(messages[code] || "Unable to sign in with Google. Please try again.") : error instanceof Error ? error : new Error("Unable to sign in with Google.");
}
