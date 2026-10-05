namespace OralExamination.Domain.Common;

public interface ILockableEntity
{
    bool IsLocked { get; set; }
}
