export function createAdminPagination<T>(
  id: string,
  itemLabel: string,
  redraw: () => void,
) {
  const pageSizeSelect = document.getElementById(`${id}-page-size`) as HTMLSelectElement | null;
  const info = document.getElementById(`${id}-pagination-info`);
  const pageLabel = document.getElementById(`${id}-page-current`);
  const previous = document.getElementById(`${id}-page-prev`) as HTMLButtonElement | null;
  const next = document.getElementById(`${id}-page-next`) as HTMLButtonElement | null;
  let page = 1;
  let pageSize = Number(pageSizeSelect?.value) || 25;

  pageSizeSelect?.addEventListener("change", () => {
    pageSize = Number(pageSizeSelect.value) || 25;
    page = 1;
    redraw();
  });
  previous?.addEventListener("click", () => {
    if (page > 1) {
      page--;
      redraw();
    }
  });
  next?.addEventListener("click", () => {
    page++;
    redraw();
  });

  return {
    reset() {
      page = 1;
    },
    slice(items: T[]): T[] {
      const total = items.length;
      const totalPages = Math.max(1, Math.ceil(total / pageSize));
      page = Math.min(page, totalPages);
      const start = total === 0 ? 0 : (page - 1) * pageSize;
      const end = Math.min(start + pageSize, total);

      if (info) info.textContent = total === 0
        ? `Showing 0 of 0 ${itemLabel}`
        : `Showing ${start + 1}-${end} of ${total} ${itemLabel}`;
      if (pageLabel) pageLabel.textContent = `Page ${page} of ${totalPages}`;
      if (previous) previous.disabled = page <= 1;
      if (next) next.disabled = page >= totalPages;

      return items.slice(start, end);
    },
  };
}
