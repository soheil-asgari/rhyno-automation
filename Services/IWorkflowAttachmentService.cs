using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using OfficeAutomation.Models;

namespace OfficeAutomation.Services;

public interface IWorkflowAttachmentService
{
    Task<WorkflowAttachment?> AddAttachmentAsync(
        WorkflowInstance instance,
        string documentType,
        int documentId,
        string userId,
        IFormFile file,
        int? workflowStepId = null,
        int? workflowDecisionId = null,
        CancellationToken cancellationToken = default);

    void ValidateWorkflowAttachment(IFormFile file);
}

public sealed class WorkflowAttachmentService : IWorkflowAttachmentService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".csv", ".zip"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/png",
        "image/jpeg",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain",
        "text/csv",
        "application/zip",
        "application/x-zip-compressed"
    };

    private const long MaxAttachmentBytes = 10 * 1024 * 1024;

    private readonly IWebHostEnvironment _environment;
    private readonly Tenancy.ITenantPathResolver? _tenantPathResolver;

    public WorkflowAttachmentService(
        IWebHostEnvironment environment,
        Tenancy.ITenantPathResolver? tenantPathResolver = null)
    {
        _environment = environment;
        _tenantPathResolver = tenantPathResolver;
    }

    public async Task<WorkflowAttachment?> AddAttachmentAsync(
        WorkflowInstance instance,
        string documentType,
        int documentId,
        string userId,
        IFormFile file,
        int? workflowStepId = null,
        int? workflowDecisionId = null,
        CancellationToken cancellationToken = default)
    {
        if (_environment == null || file == null || file.Length == 0)
        {
            return null;
        }

        ValidateWorkflowAttachment(file);

        var uploadsRoot = _tenantPathResolver?.GetWorkflowUploadRoot(
            System.IO.Path.Combine(_environment.ContentRootPath, "App_Data"), documentType, documentId)
            ?? System.IO.Path.Combine(_environment.ContentRootPath, "App_Data", "uploads", "workflow",
                documentType.ToLowerInvariant(), documentId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        System.IO.Directory.CreateDirectory(uploadsRoot);

        var extension = System.IO.Path.GetExtension(file.FileName);
        var originalName = System.IO.Path.GetFileName(file.FileName);
        var safeFileName = $"{System.Guid.NewGuid():N}{extension}";
        var physicalPath = System.IO.Path.Combine(uploadsRoot, safeFileName);

        await using (var stream = System.IO.File.Create(physicalPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var relativePath = _tenantPathResolver?.GetTenantRelativePath("uploads", "workflow",
            documentType.ToLowerInvariant(), documentId.ToString(System.Globalization.CultureInfo.InvariantCulture), safeFileName)
            ?? $"/uploads/workflow/{documentType.ToLowerInvariant()}/{documentId.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{safeFileName}";

        return new WorkflowAttachment
        {
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = workflowStepId,
            WorkflowDecisionId = workflowDecisionId,
            FileName = originalName,
            FilePath = relativePath,
            ContentType = NormalizeContentType(file.ContentType, extension),
            FileSize = file.Length,
            UploadedByUserId = userId
        };
    }

    public void ValidateWorkflowAttachment(IFormFile file)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Workflow attachment is empty.");
        }

        if (file.Length > MaxAttachmentBytes)
        {
            throw new InvalidOperationException("Workflow attachment exceeds the maximum allowed size.");
        }

        var extension = System.IO.Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Workflow attachment file type is not allowed.");
        }

        if (!string.IsNullOrWhiteSpace(file.ContentType) &&
            !AllowedContentTypes.Contains(file.ContentType))
        {
            throw new InvalidOperationException("Workflow attachment content type is not allowed.");
        }
    }

    private static string NormalizeContentType(string? contentType, string extension)
    {
        if (!string.IsNullOrWhiteSpace(contentType) && AllowedContentTypes.Contains(contentType))
        {
            return contentType;
        }

        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}