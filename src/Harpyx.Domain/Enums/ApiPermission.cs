namespace Harpyx.Domain.Enums;

[Flags]
public enum ApiPermission
{
    None = 0,
    QueryProjects = 1 << 0,
    UploadDocuments = 1 << 1,
    DeleteDocuments = 1 << 2,
    CreateProjects = 1 << 3,
    CreateWorkspaces = 1 << 4,
    DeleteProjects = 1 << 5,
    DeleteWorkspaces = 1 << 6
}
