export function attachQuantityStepper(
  input: HTMLInputElement | null,
  decrementButton: HTMLElement | null,
  incrementButton: HTMLElement | null,
): void {
  if (!input) return;
  const adjust = (amount: number) => {
    const step = Number(input.step) > 0 ? Number(input.step) : 1;
    const min = input.min === "" ? Number.NEGATIVE_INFINITY : Number(input.min);
    const max = input.max === "" ? Number.POSITIVE_INFINITY : Number(input.max);
    const current = Number(input.value) || 0;
    const next = Math.max(min, Math.min(max, current + amount * step));
    const decimals = Math.max(0, (String(step).split(".")[1] || "").length);
    input.value = String(Number(next.toFixed(decimals)));
    input.dispatchEvent(new Event("input", { bubbles: true }));
    input.dispatchEvent(new Event("change", { bubbles: true }));
  };
  decrementButton?.addEventListener("click", () => adjust(-1));
  incrementButton?.addEventListener("click", () => adjust(1));
}
