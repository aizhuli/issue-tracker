import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { DeleteProjectDialog } from "@/components/projects/DeleteProjectDialog";

// Modal uses ReactDOM.createPortal — render children inline for tests
vi.mock("react-dom", async () => {
  const actual = await vi.importActual<typeof import("react-dom")>("react-dom");
  return {
    ...actual,
    createPortal: (node: React.ReactNode) => node,
  };
});

const mockProject = {
  id: "abc123",
  slug: "my-project",
  name: "My Project",
  ownerId: "owner1",
  ownerName: "Alice",
  createdAt: "2024-01-01T00:00:00Z",
};

describe("DeleteProjectDialog", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders the project name in the body text", () => {
    render(
      <DeleteProjectDialog
        open
        project={mockProject}
        onClose={vi.fn()}
        onDeleted={vi.fn()}
      />
    );
    expect(screen.getByText("My Project", { selector: "strong" })).toBeInTheDocument();
  });

  it("Delete button is disabled until the exact project name is typed", () => {
    render(
      <DeleteProjectDialog
        open
        project={mockProject}
        onClose={vi.fn()}
        onDeleted={vi.fn()}
      />
    );
    const deleteBtn = screen.getByRole("button", { name: /^delete$/i });
    expect(deleteBtn).toBeDisabled();
  });

  it("Delete button is disabled after partial name entry", async () => {
    render(
      <DeleteProjectDialog
        open
        project={mockProject}
        onClose={vi.fn()}
        onDeleted={vi.fn()}
      />
    );
    const input = screen.getByPlaceholderText("My Project");
    await userEvent.type(input, "My Pro");
    const deleteBtn = screen.getByRole("button", { name: /^delete$/i });
    expect(deleteBtn).toBeDisabled();
  });

  it("Delete button is enabled after typing the exact project name", async () => {
    render(
      <DeleteProjectDialog
        open
        project={mockProject}
        onClose={vi.fn()}
        onDeleted={vi.fn()}
      />
    );
    const input = screen.getByPlaceholderText("My Project");
    await userEvent.type(input, "My Project");
    const deleteBtn = screen.getByRole("button", { name: /^delete$/i });
    expect(deleteBtn).not.toBeDisabled();
  });

  it("Esc keydown closes the dialog via onClose", () => {
    const onClose = vi.fn();
    render(
      <DeleteProjectDialog
        open
        project={mockProject}
        onClose={onClose}
        onDeleted={vi.fn()}
      />
    );
    fireEvent.keyDown(document, { key: "Escape" });
    expect(onClose).toHaveBeenCalledOnce();
  });

  it("calls onDeleted and onClose on successful 204 DELETE", async () => {
    const onClose = vi.fn();
    const onDeleted = vi.fn();

    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, status: 204 }));

    render(
      <DeleteProjectDialog
        open
        project={mockProject}
        onClose={onClose}
        onDeleted={onDeleted}
      />
    );

    const input = screen.getByPlaceholderText("My Project");
    await userEvent.type(input, "My Project");

    const deleteBtn = screen.getByRole("button", { name: /^delete$/i });
    await userEvent.click(deleteBtn);

    await waitFor(() => {
      expect(onDeleted).toHaveBeenCalledOnce();
      expect(onClose).toHaveBeenCalledOnce();
    });
  });

  it("shows error message on non-2xx DELETE response", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 403,
        json: () => Promise.resolve({ detail: "Not the owner" }),
      })
    );

    render(
      <DeleteProjectDialog
        open
        project={mockProject}
        onClose={vi.fn()}
        onDeleted={vi.fn()}
      />
    );

    const input = screen.getByPlaceholderText("My Project");
    await userEvent.type(input, "My Project");
    const deleteBtn = screen.getByRole("button", { name: /^delete$/i });
    await userEvent.click(deleteBtn);

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent("Not the owner");
    });
  });
});
