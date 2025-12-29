using Certio.Application.DTOs.ChangeControl;

namespace Certio.Application.Interfaces;

public interface IChangeNoticeService
{
    Task<ChangeControlSummaryDto> GetSummaryAsync(int organizationId, int matterId, CancellationToken ct = default);

    Task<ChangeNoticeDetailDto?> GetByIdAsync(int organizationId, int matterId, int changeNoticeId, CancellationToken ct = default);

    Task<ChangeControlSummaryDto> CreateDraftAsync(
        int organizationId,
        int matterId,
        int createdByUserId,
        CreateChangeNoticeRequest request,
        CancellationToken ct = default);

    Task<ChangeControlSummaryDto> UpdateDraftAsync(
        int organizationId,
        int matterId,
        int changeNoticeId,
        int modifiedByUserId,
        CreateChangeNoticeRequest request,
        CancellationToken ct = default);

    Task DeleteAsync(int organizationId, int matterId, int changeNoticeId, CancellationToken ct = default);

    Task<ChangeControlSummaryDto> SendAsync(
        int organizationId,
        int matterId,
        int changeNoticeId,
        string baseUrl,
        int sentByUserId,
        CancellationToken ct = default);

    Task<PublicChangeNoticeResponseResult> ProcessPublicResponseAsync(string token, CancellationToken ct = default);

    Task<PublicChangeNoticeResponseResult> SavePublicClarificationNoteAsync(
        string token,
        string clarificationNote,
        CancellationToken ct = default);
}


