export interface AdminDraft<T> {
  values: T;
  uploadInterrupted: boolean;
  savedAt: number;
}

export function readAdminDraft<T>(key: string): AdminDraft<T> | null {
  try {
    const raw = localStorage.getItem(key);
    return raw ? JSON.parse(raw) as AdminDraft<T> : null;
  } catch {
    return null;
  }
}

export function saveAdminDraft<T>(key: string, values: T, uploadInterrupted = false): void {
  try {
    localStorage.setItem(key, JSON.stringify({ values, uploadInterrupted, savedAt: Date.now() }));
  } catch {
    // Draft recovery is best-effort if browser storage is unavailable or full.
  }
}

export function clearAdminDraft(key: string): void {
  try {
    localStorage.removeItem(key);
  } catch {
    // Ignore storage errors; the current form can still be used.
  }
}
