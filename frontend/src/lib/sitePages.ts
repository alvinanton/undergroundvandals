import { contentApiUrl } from './server/auth';

export interface SitePageSetting {
  pageKey: string;
  status: 'Live' | 'ComingSoon' | 'Disabled';
  title: string;
  message: string;
  announcementTitle: string;
  announcementMessage: string;
  ctaLabel: string;
  ctaUrl: string;
}

const defaults = {
  home: { pageKey: 'home', status: 'Live', title: 'Underground Vandals', message: 'Documenting street art culture, preserving underground graffiti archives, featuring exclusive interviews, and offering official merchandise.', announcementTitle: '', announcementMessage: '', ctaLabel: '', ctaUrl: '' },
  archive: { pageKey: 'archive', status: 'Live', title: 'Street Archive', message: 'High-resolution database of street art, train actions, and graffiti walls worldwide.', announcementTitle: '', announcementMessage: '', ctaLabel: '', ctaUrl: '' },
  gallery: { pageKey: 'gallery', status: 'Live', title: 'Gallery & Exclusives', message: 'Exclusive artist spotlights, video premieres, and exhibition retrospectives.', announcementTitle: '', announcementMessage: '', ctaLabel: '', ctaUrl: '' },
  shop: { pageKey: 'shop', status: 'Live', title: 'Official Shop', message: 'Apparel, limited physical items, stickers, and digital assets from Underground Vandals.', announcementTitle: '', announcementMessage: '', ctaLabel: '', ctaUrl: '' },
};

export async function getSitePageSetting(pageKey: keyof typeof defaults): Promise<SitePageSetting> {
  const pages = await getSitePageSettings();
  return pages[pageKey];
}

export async function getSitePageSettings(): Promise<Record<keyof typeof defaults, SitePageSetting>> {
  try {
    const response = await fetch(`${contentApiUrl()}/api/site-pages`, { cache: 'no-store', signal: AbortSignal.timeout(5000) });
    if (!response.ok) return defaults;
    const rows = await response.json() as SitePageSetting[];
    return Object.fromEntries(Object.entries(defaults).map(([key, fallback]) => [
      key, rows.find(page => page.pageKey.toLowerCase() === key) || fallback,
    ])) as Record<keyof typeof defaults, SitePageSetting>;
  } catch {
    return defaults;
  }
}
