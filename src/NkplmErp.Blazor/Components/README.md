# Modal Components Usage Guide

## Components Created

### 1. Modal.razor
A flexible, reusable modal component with customizable size, header, body, and footer.

### 2. ConfirmDialog.razor
A specialized confirmation dialog with different types (danger, warning, info, success).

---

## Modal Component

### Basic Usage

```razor
<Modal IsVisible="@showModal" 
       Title="Edit User" 
       OnClose="CloseModal">
    <ChildContent>
        <p>Your modal content goes here</p>
    </ChildContent>
    <FooterContent>
        <button @onclick="CloseModal" class="px-4 py-2 border border-gray-300 text-gray-700 rounded-lg">
            Cancel
        </button>
        <button @onclick="Save" class="px-4 py-2 bg-blue-600 text-white rounded-lg">
            Save
        </button>
    </FooterContent>
</Modal>

@code {
    private bool showModal = false;

    private void CloseModal()
    {
        showModal = false;
    }

    private void Save()
    {
        // Save logic
        showModal = false;
    }
}
```

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `IsVisible` | bool | false | Controls modal visibility |
| `Title` | string | "" | Modal title in header |
| `ChildContent` | RenderFragment | null | Main modal content |
| `FooterContent` | RenderFragment | null | Footer content (buttons) |
| `Size` | string | "md" | Modal size: sm, md, lg, xl, 2xl, 3xl, 4xl, full |
| `ShowCloseButton` | bool | true | Show X button in header |
| `CloseOnOverlayClick` | bool | true | Close when clicking outside |
| `OnClose` | EventCallback | - | Callback when modal closes |
| `BodyClass` | string | "p-6" | Custom CSS for body |
| `FooterClass` | string | "flex justify-end gap-3" | Custom CSS for footer |
| `MaxHeight` | string | "max-h-[90vh]" | Custom max height |

### Size Examples

```razor
<!-- Small modal -->
<Modal Size="sm" IsVisible="@show" Title="Small Modal">
    <ChildContent>Content</ChildContent>
</Modal>

<!-- Large modal -->
<Modal Size="2xl" IsVisible="@show" Title="Large Modal">
    <ChildContent>Content</ChildContent>
</Modal>

<!-- Full width modal -->
<Modal Size="full" IsVisible="@show" Title="Full Modal">
    <ChildContent>Content</ChildContent>
</Modal>
```

### Without Footer

```razor
<Modal IsVisible="@show" Title="Info" ShowCloseButton="true">
    <ChildContent>
        <p>This modal has no footer, just content and a close button.</p>
    </ChildContent>
</Modal>
```

### Custom Styling

```razor
<Modal IsVisible="@show" 
       Title="Custom Styled Modal"
       BodyClass="p-8 bg-gray-50"
       FooterClass="p-6 bg-gray-100 flex justify-between">
    <ChildContent>
        <p>Custom body styling</p>
    </ChildContent>
    <FooterContent>
        <button>Left Button</button>
        <button>Right Button</button>
    </FooterContent>
</Modal>
```

---

## ConfirmDialog Component

### Basic Usage

```razor
<ConfirmDialog IsVisible="@showConfirm"
               Title="Delete User"
               Message="Are you sure you want to delete this user? This action cannot be undone."
               Type="danger"
               ConfirmText="Delete"
               OnConfirm="HandleDelete"
               OnCancel="CancelDelete"
               IsProcessing="@isDeleting" />

@code {
    private bool showConfirm = false;
    private bool isDeleting = false;

    private async Task HandleDelete()
    {
        isDeleting = true;
        // Perform delete operation
        await Task.Delay(1000);
        isDeleting = false;
        showConfirm = false;
    }

    private void CancelDelete()
    {
        showConfirm = false;
    }
}
```

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `IsVisible` | bool | false | Controls dialog visibility |
| `Title` | string | "Confirm Action" | Dialog title |
| `Subtitle` | string | "" | Optional subtitle |
| `Message` | string | "Are you sure..." | Message text |
| `MessageContent` | RenderFragment | null | Custom message markup |
| `Type` | string | "warning" | Type: danger, warning, info, success |
| `ConfirmText` | string | "Confirm" | Confirm button text |
| `CancelText` | string | "Cancel" | Cancel button text |
| `ProcessingText` | string | "Processing..." | Text while processing |
| `IsProcessing` | bool | false | Show processing state |
| `CloseOnOverlayClick` | bool | false | Close on outside click |
| `OnConfirm` | EventCallback | - | Callback when confirmed |
| `OnCancel` | EventCallback | - | Callback when cancelled |

### Type Examples

```razor
<!-- Danger (red) - for destructive actions -->
<ConfirmDialog Type="danger" 
               Title="Delete Item" 
               Message="This will permanently delete the item."
               IsVisible="@show" />

<!-- Warning (yellow) - for important actions -->
<ConfirmDialog Type="warning" 
               Title="Unsaved Changes" 
               Message="You have unsaved changes. Continue?"
               IsVisible="@show" />

<!-- Info (blue) - for informational confirmations -->
<ConfirmDialog Type="info" 
               Title="Proceed?" 
               Message="Do you want to continue with this action?"
               IsVisible="@show" />

<!-- Success (green) - for positive confirmations -->
<ConfirmDialog Type="success" 
               Title="Activate User" 
               Message="Activate this user account?"
               IsVisible="@show" />
```

### Custom Message Content

```razor
<ConfirmDialog IsVisible="@show"
               Title="Delete Multiple Items"
               Type="danger"
               OnConfirm="HandleDelete">
    <MessageContent>
        <p class="mb-2">You are about to delete <strong>@itemCount items</strong>.</p>
        <p class="text-sm text-gray-600">This action cannot be undone.</p>
    </MessageContent>
</ConfirmDialog>
```

### With Processing State

```razor
<ConfirmDialog IsVisible="@showSave"
               Title="Save Changes"
               Message="Save all changes to the database?"
               Type="info"
               ConfirmText="Save"
               ProcessingText="Saving..."
               IsProcessing="@isSaving"
               OnConfirm="SaveChanges" />

@code {
    private bool isSaving = false;

    private async Task SaveChanges()
    {
        isSaving = true;
        await Task.Delay(2000); // Simulate save
        isSaving = false;
        showSave = false;
    }
}
```

---

## Complete Example: User Form with Modal

```razor
@page "/users-example"

<button @onclick="OpenCreateModal" class="px-4 py-2 bg-blue-600 text-white rounded-lg">
    Add User
</button>

<!-- User Form Modal -->
<Modal IsVisible="@showUserModal" 
       Title="@(isEdit ? "Edit User" : "Create User")"
       Size="2xl"
       OnClose="CloseUserModal">
    <ChildContent>
        @if (errorMessage != null)
        {
            <div class="mb-4 p-4 bg-red-50 border border-red-200 rounded-lg">
                <p class="text-sm text-red-600">@errorMessage</p>
            </div>
        }

        <div class="space-y-4">
            <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Name</label>
                <input @bind="userName" type="text" class="w-full px-4 py-2 border rounded-lg" />
            </div>
            <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Email</label>
                <input @bind="userEmail" type="email" class="w-full px-4 py-2 border rounded-lg" />
            </div>
        </div>
    </ChildContent>
    <FooterContent>
        <button @onclick="CloseUserModal" 
                class="px-4 py-2 border border-gray-300 text-gray-700 rounded-lg hover:bg-gray-50">
            Cancel
        </button>
        <button @onclick="SaveUser" 
                disabled="@isSaving"
                class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50">
            @(isSaving ? "Saving..." : "Save")
        </button>
    </FooterContent>
</Modal>

<!-- Delete Confirmation -->
<ConfirmDialog IsVisible="@showDeleteConfirm"
               Title="Delete User"
               Subtitle="This action cannot be undone"
               Type="danger"
               ConfirmText="Delete"
               IsProcessing="@isDeleting"
               OnConfirm="ConfirmDelete"
               OnCancel="@(() => showDeleteConfirm = false)">
    <MessageContent>
        <p>Are you sure you want to delete <strong>@userToDelete</strong>?</p>
    </MessageContent>
</ConfirmDialog>

@code {
    private bool showUserModal = false;
    private bool showDeleteConfirm = false;
    private bool isEdit = false;
    private bool isSaving = false;
    private bool isDeleting = false;
    private string? errorMessage;
    private string userName = "";
    private string userEmail = "";
    private string userToDelete = "";

    private void OpenCreateModal()
    {
        isEdit = false;
        userName = "";
        userEmail = "";
        errorMessage = null;
        showUserModal = true;
    }

    private void CloseUserModal()
    {
        showUserModal = false;
    }

    private async Task SaveUser()
    {
        isSaving = true;
        // Simulate save
        await Task.Delay(1000);
        isSaving = false;
        showUserModal = false;
    }

    private void OpenDeleteConfirm(string name)
    {
        userToDelete = name;
        showDeleteConfirm = true;
    }

    private async Task ConfirmDelete()
    {
        isDeleting = true;
        // Simulate delete
        await Task.Delay(1000);
        isDeleting = false;
        showDeleteConfirm = false;
    }
}
```

---

## Tips

1. **Always use `@onclick:stopPropagation`** on the modal content div to prevent clicks from bubbling to the overlay
2. **Set `CloseOnOverlayClick="false"`** for ConfirmDialog to prevent accidental dismissal
3. **Use `IsProcessing` parameter** to disable buttons during async operations
4. **Customize sizes** based on content - use `sm` for alerts, `2xl` for forms
5. **Use appropriate dialog types** - `danger` for destructive actions, `warning` for important decisions

---

## Styling Customization

Both components use Tailwind CSS classes. You can customize by:

1. **Passing custom classes** via `BodyClass`, `FooterClass`, etc.
2. **Modifying the component files** directly for global changes
3. **Creating wrapper components** for specific use cases
