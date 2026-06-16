using Microsoft.AspNetCore.Components;
using NkplmErp.Shared.DTOs;
using System.Net.Http.Json;

namespace NkplmErp.Blazor.Pages;

public partial class Users
{
    [Inject] private NkplmErp.Blazor.Services.Users.UsersApiClient Api { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private NkplmErp.Blazor.Services.RoleManagement.PermissionService Permissions { get; set; } = default!;
    [Inject] private NkplmErp.Blazor.Services.RoleManagement.RoleManagementApiClient RoleApi { get; set; } = default!;

    private List<UserListItemDto> users = new();
    private IEnumerable<UserListItemDto> filteredUsers => FilterUsersList();
    private bool isLoading = true;
    private string? errorMessage;

    // Search and filter
    private string searchTerm = string.Empty;
    private string statusFilter = "all";

    // Modal state
    private bool showModal = false;
    private bool isEditMode = false;
    private bool isSaving = false;
    private string? formErrorMessage;
    private UserFormData formData = new();
    private string? editingUserId;

    // Delete confirmation
    private bool showDeleteConfirm = false;
    private bool isDeleting = false;
    private string? deleteUserId;
    private string? deleteUserName;

    // Available roles — fetched dynamically from Role Management API
    private List<string> availableRoles = new();

    protected override async Task OnInitializedAsync()
    {
        if (!Permissions.IsLoaded)
        {
            await Permissions.LoadPermissionsAsync();
        }

        if (!Permissions.CanView("Users"))
        {
            Navigation.NavigateTo("/dashboard");
            return;
        }

        await LoadUsers();
        await LoadRoles();
    }

    private async Task LoadRoles()
    {
        try
        {
            var roles = await RoleApi.GetAllRolesAsync();
            availableRoles = roles.Select(r => r.RoleName).OrderBy(n => n).ToList();
        }
        catch
        {
            // Keep empty list on error — user will see no checkboxes
            availableRoles = new();
        }
    }

    private async Task LoadUsers()
    {
        isLoading = true;
        errorMessage = null;

        try
        {
            var response = await Api.Client.GetAsync("api/v1/users");
            
            if (response.IsSuccessStatusCode)
            {
                users = await response.Content.ReadFromJsonAsync<List<UserListItemDto>>() ?? new();
            }
            else
            {
                errorMessage = $"Failed to load users: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error loading users: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }

    private IEnumerable<UserListItemDto> FilterUsersList()
    {
        var filtered = users.AsEnumerable();

        // Filter by search term
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            filtered = filtered.Where(u => 
                (u.FullName != null && u.FullName.ToLower().Contains(term)) || 
                (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        // Filter by status
        if (statusFilter == "active")
        {
            filtered = filtered.Where(u => u.IsActive);
        }
        else if (statusFilter == "inactive")
        {
            filtered = filtered.Where(u => !u.IsActive);
        }

        return filtered;
    }

    private void FilterUsers()
    {
        // Trigger re-render
        StateHasChanged();
    }

    private void OpenCreateModal()
    {
        isEditMode = false;
        editingUserId = null;
        formData = new UserFormData { IsActive = true };
        formErrorMessage = null;
        showModal = true;
    }

    private async Task OpenEditModal(string userId)
    {
        isEditMode = true;
        editingUserId = userId;
        formErrorMessage = null;

        try
        {
            var user = await Api.Client.GetFromJsonAsync<UserResponseDto>($"api/v1/users/{userId}");
            if (user != null)
            {
                formData = new UserFormData
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    IsActive = user.IsActive,
                    AssignedGauge = user.AssignedGauge,
                    Roles = user.Roles.ToList()
                };
                showModal = true;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error loading user: {ex.Message}";
        }
    }

    private void CloseModal()
    {
        showModal = false;
        formData = new();
        formErrorMessage = null;
    }

    private async Task SaveUser()
    {
        isSaving = true;
        formErrorMessage = null;

        try
        {
            HttpResponseMessage response;

            if (isEditMode && editingUserId != null)
            {
                // Update existing user
                var updateDto = new UpdateUserDto
                {
                    FirstName = formData.FirstName,
                    LastName = formData.LastName,
                    Email = formData.Email,
                    IsActive = formData.IsActive,
                    AssignedGauge = formData.AssignedGauge,
                    Roles = formData.Roles
                };

                response = await Api.Client.PutAsJsonAsync($"api/v1/users/{editingUserId}", updateDto);
            }
            else
            {
                // Create new user
                var createDto = new CreateUserDto
                {
                    FirstName = formData.FirstName,
                    LastName = formData.LastName,
                    Email = formData.Email,
                    Password = formData.Password,
                    IsActive = formData.IsActive,
                    AssignedGauge = formData.AssignedGauge,
                    Roles = formData.Roles
                };

                response = await Api.Client.PostAsJsonAsync("api/v1/users", createDto);
            }

            if (response.IsSuccessStatusCode)
            {
                await LoadUsers();
                CloseModal();
            }
            else
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                formErrorMessage = error?.Message ?? $"Error: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            formErrorMessage = $"Error saving user: {ex.Message}";
        }
        finally
        {
            isSaving = false;
        }
    }

    private void ToggleRole(string role, bool isChecked)
    {
        if (isChecked)
        {
            if (!formData.Roles.Contains(role))
            {
                formData.Roles.Add(role);
            }
        }
        else
        {
            formData.Roles.Remove(role);
        }
    }

    private void OpenDeleteConfirm(string userId, string userName)
    {
        deleteUserId = userId;
        deleteUserName = userName;
        showDeleteConfirm = true;
    }

    private void CloseDeleteConfirm()
    {
        showDeleteConfirm = false;
        deleteUserId = null;
        deleteUserName = null;
    }

    private async Task ConfirmDelete()
    {
        if (deleteUserId == null) return;

        isDeleting = true;

        try
        {
            var response = await Api.Client.DeleteAsync($"api/v1/users/{deleteUserId}");

            if (response.IsSuccessStatusCode)
            {
                await LoadUsers();
                CloseDeleteConfirm();
            }
            else
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                errorMessage = error?.Message ?? $"Error deleting user: {response.StatusCode}";
                CloseDeleteConfirm();
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error deleting user: {ex.Message}";
            CloseDeleteConfirm();
        }
        finally
        {
            isDeleting = false;
        }
    }

    private class UserFormData
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string? AssignedGauge { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    private class ErrorResponse
    {
        public string Message { get; set; } = string.Empty;
    }
}
