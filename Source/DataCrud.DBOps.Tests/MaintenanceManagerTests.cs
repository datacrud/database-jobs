using System;
using System.Threading.Tasks;
using DataCrud.DBOps.Core;
using DataCrud.DBOps.Core.Models;
using DataCrud.DBOps.Core.Providers;
using DataCrud.DBOps.Core.Storage;
using Moq;
using Xunit;
using FluentAssertions;

namespace DataCrud.DBOps.Tests
{
    public class MaintenanceManagerTests
    {
        private readonly Mock<IDatabaseProvider> _mockProvider;
        private readonly Mock<IJobStorage> _mockStorage;
        private readonly MaintenanceManager _sut;

        public MaintenanceManagerTests()
        {
            _mockProvider = new Mock<IDatabaseProvider>();
            _mockStorage = new Mock<IJobStorage>();
            _sut = new MaintenanceManager(_mockStorage.Object, _mockProvider.Object);
        }

        [Fact]
        public async Task RunAsync_ShouldInvokeShrink_WhenRequestedAndSupported()
        {
            // Arrange
            var dbName = "TestDB";
            _mockProvider.Setup(p => p.Capabilities).Returns(ProviderCapabilities.Shrink);

            // Act
            await _sut.RunAsync(dbName, backup: false, shrink: true, index: false, reorganize: false, cleanup: false);

            // Assert
            _mockProvider.Verify(p => p.ShrinkAsync(dbName, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunAsync_ShouldNOTInvokeShrink_WhenRequestedButNOTSupported()
        {
            // Arrange
            var dbName = "TestDB";
            _mockProvider.Setup(p => p.Capabilities).Returns(ProviderCapabilities.None);

            // Act
            await _sut.RunAsync(dbName, backup: false, shrink: true, index: false, reorganize: false, cleanup: false);

            // Assert
            _mockProvider.Verify(p => p.ShrinkAsync(dbName, It.IsAny<System.Threading.CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_ShouldInvokeBackup_WhenRequestedAndSupported()
        {
            // Arrange
            var dbName = "TestDB";
            var backupDir = "C:\\TempBackups";
            _mockProvider.Setup(p => p.Capabilities).Returns(ProviderCapabilities.Backup);

            // Act
            await _sut.RunAsync(dbName, backup: true, shrink: false, index: false, reorganize: false, cleanup: false, backupDir: backupDir);

            // Assert
            _mockProvider.Verify(p => p.BackupAsync(dbName, backupDir, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunAsync_ShouldThrow_WhenBackupRequestedButNoDirectory()
        {
            // Arrange
            _mockProvider.Setup(p => p.Capabilities).Returns(ProviderCapabilities.Backup);

            // Act
            Func<Task> act = () => _sut.RunAsync("TestDB", backup: true, shrink: false, index: false, reorganize: false, cleanup: false, backupDir: null);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Backup directory must be specified*");
        }

        [Fact]
        public async Task RunAsync_ShouldInitializeStorageOnStart()
        {
            // Act
            await _sut.RunAsync("TestDB", false, false, false, false, false);

            // Assert
            _mockStorage.Verify(s => s.InitializeAsync(null), Times.Once);
        }

        [Fact]
        public async Task RunAsync_ShouldInvokeReorganize_WhenRequestedAndSupported()
        {
            // Arrange
            var dbName = "TestDB";
            _mockProvider.Setup(p => p.Capabilities).Returns(ProviderCapabilities.Reorganize);

            // Act
            await _sut.RunAsync(dbName, backup: false, shrink: false, index: false, reorganize: true, cleanup: false);

            // Assert
            _mockProvider.Verify(p => p.ReorganizeAsync(dbName, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }
    }
}
