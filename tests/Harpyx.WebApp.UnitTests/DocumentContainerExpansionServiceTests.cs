using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;
using Harpyx.Infrastructure.Services;
using System.IO.Compression;

namespace Harpyx.WebApp.UnitTests;

public class DocumentContainerExpansionServiceTests
{
    [Fact]
    public async Task ExpandAsync_ArchiveWithUnsupportedEntry_CreatesUnsupportedChildWithoutQueue()
    {
        var storage = new Mock<IStorageService>();
        var documents = new Mock<IDocumentRepository>();
        var queue = new Mock<IJobQueue>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var added = new List<Document>();

        storage.Setup(s => s.OpenReadAsync("root-storage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateZip(("malware.exe", new byte[] { 0x01, 0x02 })));
        documents.Setup(d => d.GetByParentDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Document>());
        documents.Setup(d => d.GetExtractionStatsByRootAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 0L));
        documents.Setup(d => d.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>((doc, _) => added.Add(doc))
            .Returns(Task.CompletedTask);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = new DocumentContainerExpansionService(storage.Object, documents.Object, queue.Object, unitOfWork.Object);
        var container = CreateContainerDocument(DocumentContainerType.Archive);

        var result = await service.ExpandAsync(container, CreateRuntimeSettings(), CancellationToken.None);

        result.HasWarnings.Should().BeFalse();
        added.Should().HaveCount(1);
        var child = added[0];
        child.ParentDocumentId.Should().Be(container.Id);
        child.RootContainerDocumentId.Should().Be(container.Id);
        child.OriginatingUploadId.Should().Be(container.Id);
        child.NestingLevel.Should().Be(1);
        child.ContainerPath.Should().Be("malware.exe");
        child.State.Should().Be(DocumentState.Completed);
        child.ExtractionState.Should().Be(DocumentExtractionState.Unsupported);
        child.StorageKey.Should().BeEmpty();
        queue.Verify(q => q.EnqueueParseJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        storage.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExpandAsync_ArchiveWithSupportedEntry_CreatesQueuedChildAndEnqueuesJob()
    {
        var storage = new Mock<IStorageService>();
        var documents = new Mock<IDocumentRepository>();
        var queue = new Mock<IJobQueue>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var added = new List<Document>();

        storage.Setup(s => s.OpenReadAsync("root-storage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateZip(("docs/readme.txt", System.Text.Encoding.UTF8.GetBytes("hello"))));
        storage.Setup(s => s.UploadAsync("readme.txt", It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ReturnsAsync("child-storage");
        documents.Setup(d => d.GetByParentDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Document>());
        documents.Setup(d => d.GetExtractionStatsByRootAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 0L));
        documents.Setup(d => d.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>((doc, _) => added.Add(doc))
            .Returns(Task.CompletedTask);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = new DocumentContainerExpansionService(storage.Object, documents.Object, queue.Object, unitOfWork.Object);
        var container = CreateContainerDocument(DocumentContainerType.Archive);

        var result = await service.ExpandAsync(container, CreateRuntimeSettings(), CancellationToken.None);

        result.HasWarnings.Should().BeFalse();
        added.Should().HaveCount(1);
        var child = added[0];
        child.ParentDocumentId.Should().Be(container.Id);
        child.RootContainerDocumentId.Should().Be(container.Id);
        child.OriginatingUploadId.Should().Be(container.Id);
        child.NestingLevel.Should().Be(1);
        child.ContainerPath.Should().Be("docs/readme.txt");
        child.FileName.Should().Be("readme.txt");
        child.State.Should().Be(DocumentState.Queued);
        child.ExtractionState.Should().Be(DocumentExtractionState.Pending);
        child.StorageKey.Should().Be("child-storage");
        queue.Verify(q => q.EnqueueParseJobAsync(child.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Document CreateContainerDocument(DocumentContainerType type)
        => new()
        {
            Id = Guid.NewGuid(),
            UploadedByUserId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            FileName = type == DocumentContainerType.Email ? "mail.eml" : "archive.zip",
            ContentType = type == DocumentContainerType.Email ? "message/rfc822" : "application/zip",
            SizeBytes = 1024,
            StorageKey = "root-storage",
            State = DocumentState.Processing,
            IsContainer = true,
            ContainerType = type,
            NestingLevel = 0,
            OriginatingUploadId = null,
            RootContainerDocumentId = null
        };

    private static PlatformSettingsDto CreateRuntimeSettings()
        => new(
            "default system prompt",
            16000,
            16000,
            20,
            20,
            30,
            true,
            true,
            true,
            25L * 1024L * 1024L,
            3,
            500L * 1024L * 1024L,
            200,
            100L * 1024L * 1024L,
            6,
            10000,
            60,
            24,
            24,
            12,
            true,
            300,
            true,
            true,
            true);

    private static Stream CreateZip(params (string EntryName, byte[] Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                var zipEntry = archive.CreateEntry(entry.EntryName);
                using var entryStream = zipEntry.Open();
                entryStream.Write(entry.Content, 0, entry.Content.Length);
            }
        }

        stream.Position = 0;
        return stream;
    }
}
