using ErikLieben.FA.ES.EventStreamManagement.Cutover;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Cutover;

public class MigrationPhaseTests
{
    public class EnumValues
    {
        [Fact]
        public void Should_have_normal_phase()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationPhase>(MigrationPhase.Normal));
            Assert.Equal(0, (int)MigrationPhase.Normal);
        }

        [Fact]
        public void Should_have_dual_write_phase()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationPhase>(MigrationPhase.DualWrite));
            Assert.Equal(1, (int)MigrationPhase.DualWrite);
        }

        [Fact]
        public void Should_have_dual_read_phase()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationPhase>(MigrationPhase.DualRead));
            Assert.Equal(2, (int)MigrationPhase.DualRead);
        }

        [Fact]
        public void Should_have_cutover_phase()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationPhase>(MigrationPhase.Cutover));
            Assert.Equal(3, (int)MigrationPhase.Cutover);
        }

        [Fact]
        public void Should_have_book_closed_phase()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationPhase>(MigrationPhase.BookClosed));
            Assert.Equal(4, (int)MigrationPhase.BookClosed);
        }
    }

    public class EnumCount
    {
        [Fact]
        public void Should_have_expected_number_of_values()
        {
            // Arrange
            var values = Enum.GetValues<MigrationPhase>();

            // Assert
            Assert.Equal(5, values.Length);
        }
    }

    public class PhaseOrderTests
    {
        [Fact]
        public void Should_have_phases_in_correct_order()
        {
            // Arrange
            var phases = new[]
            {
                MigrationPhase.Normal,
                MigrationPhase.DualWrite,
                MigrationPhase.DualRead,
                MigrationPhase.Cutover,
                MigrationPhase.BookClosed
            };

            // Assert
            for (var i = 0; i < phases.Length; i++)
            {
                Assert.Equal(i, (int)phases[i]);
            }
        }

        [Fact]
        public void Should_allow_comparison_of_phases()
        {
            // Assert
            Assert.True(MigrationPhase.Normal < MigrationPhase.DualWrite);
            Assert.True(MigrationPhase.DualWrite < MigrationPhase.DualRead);
            Assert.True(MigrationPhase.DualRead < MigrationPhase.Cutover);
            Assert.True(MigrationPhase.Cutover < MigrationPhase.BookClosed);
        }
    }
}
