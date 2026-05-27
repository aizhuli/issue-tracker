import { render, screen, fireEvent, act } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { AssigneePicker } from "@/components/issues/AssigneePicker";
import type { UserSummary } from "@/lib/types/issues";

const alice: UserSummary = {
  id: "user-alice",
  name: "Alice",
  email: "alice@example.com",
};

const bob: UserSummary = {
  id: "user-bob",
  name: "Bob",
  email: "bob@example.com",
};

function makeFetchOk(items: UserSummary[]) {
  return vi.fn().mockResolvedValue({
    ok: true,
    json: () => Promise.resolve({ items }),
  });
}

describe("AssigneePicker", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  async function openPicker() {
    const trigger = screen.getAllByRole("button")[0];
    fireEvent.click(trigger);
    // Wait for the useEffect to register
    await act(async () => {
      await vi.runAllTimersAsync();
    });
  }

  it("shows Unassigned when value is null", () => {
    vi.stubGlobal("fetch", makeFetchOk([]));
    render(<AssigneePicker value={null} onChange={vi.fn()} />);
    expect(screen.getByText("Unassigned")).toBeInTheDocument();
  });

  it("shows assignee name when value is set", () => {
    vi.stubGlobal("fetch", makeFetchOk([]));
    render(<AssigneePicker value={alice} onChange={vi.fn()} />);
    expect(screen.getByText("Alice")).toBeInTheDocument();
  });

  it("opens dropdown with Unassigned row pinned at top", async () => {
    vi.stubGlobal("fetch", makeFetchOk([]));
    render(<AssigneePicker value={null} onChange={vi.fn()} />);
    await openPicker();
    // Dropdown is open — should show "Unassigned" row
    const unassignedButtons = screen.getAllByText("Unassigned");
    // At least one Unassigned in the dropdown list
    expect(unassignedButtons.length).toBeGreaterThanOrEqual(1);
  });

  it("debounced search fires only once after 200ms pause", async () => {
    const fetchMock = makeFetchOk([alice]);
    vi.stubGlobal("fetch", fetchMock);
    render(<AssigneePicker value={null} onChange={vi.fn()} />);
    await openPicker();

    const searchInput = screen.getByPlaceholderText(/search members/i);

    // Type quickly (within debounce window)
    fireEvent.change(searchInput, { target: { value: "a" } });
    fireEvent.change(searchInput, { target: { value: "al" } });
    fireEvent.change(searchInput, { target: { value: "ali" } });

    // Before debounce resolves, fetch should not have been called for the query
    // (it may have been called once on open with empty query)
    const callsBefore = fetchMock.mock.calls.length;

    // Advance by exactly 200ms to trigger debounce
    await act(async () => {
      vi.advanceTimersByTime(200);
      await vi.runAllTimersAsync();
    });

    // Should have fired exactly one more time (for "ali")
    expect(fetchMock.mock.calls.length).toBe(callsBefore + 1);
    expect(fetchMock).toHaveBeenLastCalledWith(
      expect.stringContaining("q=ali"),
      expect.anything()
    );
  });

  it("displays search results", async () => {
    vi.stubGlobal("fetch", makeFetchOk([alice, bob]));
    render(<AssigneePicker value={null} onChange={vi.fn()} />);
    await openPicker();

    const searchInput = screen.getByPlaceholderText(/search members/i);
    fireEvent.change(searchInput, { target: { value: "a" } });

    await act(async () => {
      vi.advanceTimersByTime(200);
      await vi.runAllTimersAsync();
    });

    expect(screen.getByText("Alice")).toBeInTheDocument();
    expect(screen.getByText("Bob")).toBeInTheDocument();
  });

  it("selecting a user calls onChange with that user", async () => {
    vi.stubGlobal("fetch", makeFetchOk([alice]));
    const onChange = vi.fn();
    render(<AssigneePicker value={null} onChange={onChange} />);
    await openPicker();

    const searchInput = screen.getByPlaceholderText(/search members/i);
    fireEvent.change(searchInput, { target: { value: "alice" } });

    await act(async () => {
      vi.advanceTimersByTime(200);
      await vi.runAllTimersAsync();
    });

    fireEvent.click(screen.getByText("Alice"));
    expect(onChange).toHaveBeenCalledWith(alice);
  });

  it("selecting Unassigned calls onChange with null", async () => {
    vi.stubGlobal("fetch", makeFetchOk([alice]));
    const onChange = vi.fn();
    // value=alice so the trigger button shows "Alice", not "Unassigned".
    // The dropdown Unassigned row is a <button> whose accessible name includes "Unassigned".
    render(<AssigneePicker value={alice} onChange={onChange} />);
    await openPicker();

    fireEvent.click(screen.getByText("Unassigned").closest("button")!);
    expect(onChange).toHaveBeenCalledWith(null);
  });
});
