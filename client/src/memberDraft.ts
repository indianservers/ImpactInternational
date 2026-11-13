export async function memberDraft(
  key: string,
  action: "get" | "put" | "delete",
  value?: unknown,
) {
  const db = await new Promise<IDBDatabase>((resolve, reject) => {
    const r = indexedDB.open("impact-member-registration", 1);
    r.onupgradeneeded = () => r.result.createObjectStore("drafts");
    r.onsuccess = () => resolve(r.result);
    r.onerror = () => reject(r.error);
  });
  try {
    return await new Promise<any>((resolve, reject) => {
      const tx = db.transaction(
          "drafts",
          action === "get" ? "readonly" : "readwrite",
        ),
        s = tx.objectStore("drafts"),
        r =
          action === "get"
            ? s.get(key)
            : action === "put"
              ? s.put(value, key)
              : s.delete(key);
      tx.oncomplete = () => resolve(r.result);
      tx.onerror = () => reject(tx.error);
      tx.onabort = () => reject(tx.error);
    });
  } finally {
    db.close();
  }
}
