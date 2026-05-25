import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ProjectFormModal } from "@/components/projects/ProjectFormModal";

// Modal uses ReactDOM.createPortal — render children inline for tests
vi.mock("react-dom", async () => {
  const actual = await vi.importActual<typeof import("react-dom")>("react-dom");
  return {
    ...actual,
    createPortal: (node: React.ReactNode) => node,
  };
});

describe("ProjectFormModal — create mode", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  function renderCreate() {
    return render(
      <ProjectFormModal open mode="create" onClose={vi.fn()} onSaved={vi.fn()} />
    );
  }

  it("slug auto-fills from name when user has not touched the slug field", async () => {
    renderCreate();
    const nameInput = screen.getByLabelText(/^name$/i);
    fireEvent.change(nameInput, { target: { value: "My Awesome Project" } });
    const slugInput = screen.getByLabelText(/^slug/i) as HTMLInputElement;
    expect(slugInput.value).toBe("my-awesome-project");
  });

  it("slug does not auto-fill after user manually edits the slug field", async () => {
    renderCreate();
    const slugInput = screen.getByLabelText(/^slug/i) as HTMLInputElement;
    const nameInput = screen.getByLabelText(/^name$/i);

    // User touches slug first
    fireEvent.change(slugInput, { target: { value: "custom-slug" } });
    // Then types in name
    fireEvent.change(nameInput, { target: { value: "Different Name" } });

    expect(slugInput.value).toBe("custom-slug");
  });

  it("submit button is disabled when slug status is not available", () => {
    renderCreate();
    const submitBtn = screen.getByRole("button", { name: /create project/i });
    expect(submitBtn).toBeDisabled();
  });

  it("debounced availability check fires once after 400ms on valid slug", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      json: () => Promise.resolve({ available: true, slug: "my-project" }),
    });
    vi.stubGlobal("fetch", fetchMock);

    renderCreate();
    const nameInput = screen.getByLabelText(/^name$/i);
    fireEvent.change(nameInput, { target: { value: "My Project" } });

    // Slug should be "my-project" now — availability check debounced
    expect(fetchMock).not.toHaveBeenCalled();

    await act(async () => {
      vi.advanceTimersByTime(400);
    });

    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining("slug-availability"),
      expect.anything()
    );
  });

  it("shows Available indicator after availability check returns true", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        json: () => Promise.resolve({ available: true, slug: "my-project" }),
      })
    );

    renderCreate();
    const nameInput = screen.getByLabelText(/^name$/i);
    fireEvent.change(nameInput, { target: { value: "My Project" } });

    // runAllTimersAsync fires the debounce setTimeout AND flushes the resulting fetch Promise
    await act(async () => {
      await vi.runAllTimersAsync();
    });

    expect(screen.getByText(/available/i)).toBeInTheDocument();
  });

  it("shows Already taken indicator after availability check returns taken", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        json: () => Promise.resolve({ available: false, reason: "taken", slug: "my-project" }),
      })
    );

    renderCreate();
    const nameInput = screen.getByLabelText(/^name$/i);
    fireEvent.change(nameInput, { target: { value: "My Project" } });

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    expect(screen.getByText(/already taken/i)).toBeInTheDocument();
  });

  it("re-flags slug as taken when submit returns 409", async () => {
    // First fetch: availability check → available
    // Second fetch: POST → 409 slug already_exists
    let callCount = 0;
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(() => {
        callCount++;
        if (callCount === 1) {
          return Promise.resolve({
            json: () => Promise.resolve({ available: true, slug: "my-project" }),
          });
        }
        return Promise.resolve({
          ok: false,
          status: 409,
          json: () =>
            Promise.resolve({
              errorCode: "projects:project:slug:already_exists",
              detail: "Slug already taken",
            }),
        });
      })
    );

    renderCreate();
    const nameInput = screen.getByLabelText(/^name$/i);
    fireEvent.change(nameInput, { target: { value: "My Project" } });

    // Fire availability check and flush its Promise
    await act(async () => {
      await vi.runAllTimersAsync();
    });

    expect(screen.getByText(/available/i)).toBeInTheDocument();

    // Use fireEvent (not userEvent) to avoid internal timer conflicts with fake timers
    const submitBtn = screen.getByRole("button", { name: /create project/i });
    fireEvent.click(submitBtn);

    // Flush POST fetch Promise
    await act(async () => {
      await vi.runAllTimersAsync();
    });

    expect(screen.getByText(/already taken/i)).toBeInTheDocument();
  });
});

describe("ProjectFormModal — edit mode", () => {
  const existingProject = {
    id: "proj1",
    slug: "existing-slug",
    name: "Existing Project",
    ownerId: "owner1",
    ownerName: "Alice",
    createdAt: "2024-01-01T00:00:00Z",
    description: "Original description",
    updatedAt: "2024-01-02T00:00:00Z",
  };

  it("slug field is read-only in edit mode", () => {
    render(
      <ProjectFormModal
        open
        mode="edit"
        project={existingProject}
        onClose={vi.fn()}
        onSaved={vi.fn()}
      />
    );
    const slugInput = screen.getByLabelText(/^slug/i) as HTMLInputElement;
    expect(slugInput).toHaveAttribute("readonly");
    expect(slugInput.value).toBe("existing-slug");
  });

  it("shows '(cannot be changed)' hint next to slug label", () => {
    render(
      <ProjectFormModal
        open
        mode="edit"
        project={existingProject}
        onClose={vi.fn()}
        onSaved={vi.fn()}
      />
    );
    expect(screen.getByText(/cannot be changed/i)).toBeInTheDocument();
  });

  it("submit button is enabled when name is filled in edit mode", () => {
    render(
      <ProjectFormModal
        open
        mode="edit"
        project={existingProject}
        onClose={vi.fn()}
        onSaved={vi.fn()}
      />
    );
    const submitBtn = screen.getByRole("button", { name: /save changes/i });
    expect(submitBtn).not.toBeDisabled();
  });
});
