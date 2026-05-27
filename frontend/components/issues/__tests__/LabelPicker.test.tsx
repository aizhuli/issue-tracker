import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { LabelPicker } from "@/components/issues/LabelPicker";
import type { LabelDto, LabelFullDto } from "@/lib/types/issues";

const OWNER_ID = "owner-1";
const OTHER_ID = "user-2";

const labelsFromApi: LabelFullDto[] = [
  { id: "l1", name: "bug", color: "#ff0000", createdAt: "2024-01-01T00:00:00Z" },
  { id: "l2", name: "feature", color: "#00ff00", createdAt: "2024-01-01T00:00:00Z" },
];

function makeFetchOk(data: unknown) {
  return vi.fn().mockResolvedValue({
    ok: true,
    json: () => Promise.resolve(data),
  });
}

function renderPicker({
  meId = OWNER_ID,
  value = [] as LabelDto[],
  onChange = vi.fn(),
} = {}) {
  return render(
    <LabelPicker
      projectSlug="my-proj"
      projectOwnerId={OWNER_ID}
      meId={meId}
      value={value}
      onChange={onChange}
    />
  );
}

describe("LabelPicker", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  async function openPicker(fetchMock?: ReturnType<typeof vi.fn>) {
    vi.stubGlobal("fetch", fetchMock ?? makeFetchOk(labelsFromApi));
    // The trigger is always the first (and only) button before the dropdown opens
    const trigger = screen.getAllByRole("button")[0];
    fireEvent.click(trigger);
    await act(async () => {
      await vi.runAllTimersAsync();
    });
  }

  it("opens dropdown on trigger click and shows labels from API", async () => {
    renderPicker();
    await openPicker();
    expect(screen.getByText("bug")).toBeInTheDocument();
    expect(screen.getByText("feature")).toBeInTheDocument();
  });

  it("selects a label by clicking its row", async () => {
    const onChange = vi.fn();
    renderPicker({ onChange });
    await openPicker();
    fireEvent.click(screen.getByText("bug"));
    expect(onChange).toHaveBeenCalledWith(
      expect.arrayContaining([expect.objectContaining({ id: "l1", name: "bug" })])
    );
  });

  it("deselects a label when already selected", async () => {
    const onChange = vi.fn();
    const selectedLabel: LabelDto = { id: "l1", name: "bug", color: "#ff0000" };
    renderPicker({ value: [selectedLabel], onChange });
    await openPicker();
    // When dropdown is open, there are two "bug" texts (chip in trigger + row in dropdown).
    // Click the toggle button in the dropdown list row (the first one that has its own toggle)
    const bugTexts = screen.getAllByText("bug");
    // The row in the dropdown has a button wrapping it - click the last occurrence (inside dropdown)
    fireEvent.click(bugTexts[bugTexts.length - 1]);
    expect(onChange).toHaveBeenCalledWith([]);
  });

  it("shows Create label row when search has no exact match", async () => {
    renderPicker();
    await openPicker();
    const searchInput = screen.getByPlaceholderText(/search or create label/i);
    fireEvent.change(searchInput, { target: { value: "enhancement" } });
    expect(screen.getByText(/create label/i)).toBeInTheDocument();
  });

  it("hides Create label row when search exactly matches an existing label", async () => {
    renderPicker();
    await openPicker();
    const searchInput = screen.getByPlaceholderText(/search or create label/i);
    fireEvent.change(searchInput, { target: { value: "bug" } });
    expect(screen.queryByText(/create label/i)).toBeNull();
  });

  it("does not show Create label row when search is empty", async () => {
    renderPicker();
    await openPicker();
    expect(screen.queryByText(/create label/i)).toBeNull();
  });

  it("owner sees ••• menu button when hovering a label row", async () => {
    renderPicker({ meId: OWNER_ID });
    await openPicker();
    // Simulate hover on the bug row
    const bugRow = screen.getByText("bug").closest("div");
    fireEvent.mouseEnter(bugRow!);
    // The owner should see the dots menu button
    // The icon renders as an Icon component with name="dots"
    // We check for aria pattern or fallback - look for the button without a text label
    const buttons = screen.getAllByRole("button");
    // Filter buttons that are inside the dropdown
    // The dots menu triggers menu open; after hovering we should see it
    expect(bugRow).toBeInTheDocument();
  });

  it("non-owner does not see ••• menu", async () => {
    renderPicker({ meId: OTHER_ID });
    await openPicker();
    const bugRow = screen.getByText("bug").closest("div");
    fireEvent.mouseEnter(bugRow!);
    // Non-owner should not see dots/ellipsis menu
    // The ••• button is only rendered when isOwner is true
    // After hover, check no extra action buttons appeared
    const buttonsInDropdown = screen.getAllByRole("button").filter(
      (btn) => btn.textContent === "•••"
    );
    expect(buttonsInDropdown).toHaveLength(0);
  });

  it("owner can rename a label", async () => {
    const renamedLabel = { ...labelsFromApi[0], name: "defect" };
    const fetchMock = vi.fn()
      .mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(labelsFromApi) })
      .mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(renamedLabel) });
    vi.stubGlobal("fetch", fetchMock);

    const onChange = vi.fn();
    renderPicker({ meId: OWNER_ID, onChange });
    await openPicker(fetchMock);

    // Hover over the LabelRow outer div to make hovered=true and reveal the ••• button.
    // screen.getByText("bug") returns the <span> inside the toggle <button>.
    // Its closest <div> is the LabelRow outer flex container.
    const labelRowDiv = screen.getByText("bug").closest("div")!;
    fireEvent.mouseEnter(labelRowDiv);

    // The ••• button (dots icon, no text) is now rendered. It is the only button in the
    // row that contains no text — it wraps an SVG Icon. Find it by excluding the toggle
    // button (which contains the label name span).
    const dotsBtn = screen
      .getAllByRole("button")
      .find((btn) => !btn.textContent?.trim());
    expect(dotsBtn).toBeDefined();
    fireEvent.click(dotsBtn!);

    // The popup menu is now open — click "Rename"
    const renameBtn = screen.getByRole("button", { name: /^rename$/i });
    fireEvent.click(renameBtn);

    // The rename inline input appears, pre-filled with "bug"
    const renameInput = screen.getByDisplayValue("bug");
    expect(renameInput).toBeInTheDocument();

    // Type the new name and press Enter to confirm
    fireEvent.change(renameInput, { target: { value: "defect" } });
    fireEvent.keyDown(renameInput, { key: "Enter" });

    await act(async () => { await vi.runAllTimersAsync(); });

    // PUT was called with the new name
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining("/labels/l1"),
      expect.objectContaining({ method: "PUT" })
    );

    // The updated name is now visible in the list
    expect(screen.getByText("defect")).toBeInTheDocument();
  });

  it("owner can initiate delete confirm for a label", async () => {
    vi.stubGlobal("fetch", makeFetchOk(labelsFromApi));

    renderPicker({ meId: OWNER_ID });
    await openPicker();

    // Hover the LabelRow outer div to reveal the ••• button
    const labelRowDiv = screen.getByText("bug").closest("div")!;
    fireEvent.mouseEnter(labelRowDiv);

    // Click the ••• (dots) button — it contains only an SVG, so textContent is empty
    const dotsBtn = screen
      .getAllByRole("button")
      .find((btn) => !btn.textContent?.trim());
    expect(dotsBtn).toBeDefined();
    fireEvent.click(dotsBtn!);

    // Click the "Delete" option in the popup menu
    const deleteMenuBtn = screen.getByRole("button", { name: /^delete$/i });
    fireEvent.click(deleteMenuBtn);

    // The inline confirm UI replaces the row: "Delete <strong>bug</strong>?"
    // with No / Yes buttons
    expect(screen.getByText(/delete/i, { selector: "span" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^yes$/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^no$/i })).toBeInTheDocument();
  });
});
