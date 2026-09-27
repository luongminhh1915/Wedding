using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.WeddingTools.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.WeddingTools.Queries.GetChecklist;

public record GetChecklistQuery : IRequest<List<ChecklistTaskDto>>;

public class GetChecklistQueryHandler : IRequestHandler<GetChecklistQuery, List<ChecklistTaskDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetChecklistQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<ChecklistTaskDto>> Handle(GetChecklistQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để xem danh sách công việc cưới.");

        var tasks = await _context.ChecklistTasks
            .Where(t => t.CustomerId == userId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        // Nếu người dùng mới chưa có checklist, tự động tạo seed các công việc chuẩn 12 tháng
        if (tasks.Count == 0)
        {
            var seedTasks = new List<ChecklistTask>
            {
                ChecklistTask.Create(userId, "Gặp gỡ hai bên gia đình & thống nhất ngày cưới", "9 - 12 Tháng"),
                ChecklistTask.Create(userId, "Xác định tổng ngân sách cưới và phong cách mong muốn", "9 - 12 Tháng"),
                ChecklistTask.Create(userId, "Tìm và đặt cọc trung tâm tiệc cưới / nhà hàng", "9 - 12 Tháng"),
                ChecklistTask.Create(userId, "Lên danh sách khách mời dự kiến ban đầu", "6 - 9 Tháng"),
                ChecklistTask.Create(userId, "Chọn đơn vị trang trí tiệc cưới & hoa tươi", "6 - 9 Tháng"),
                ChecklistTask.Create(userId, "Chụp album ảnh cưới Pre-Wedding & chọn váy cưới", "3 - 6 Tháng"),
                ChecklistTask.Create(userId, "Chọn nhẫn cưới và trang sức hồi môn", "3 - 6 Tháng"),
                ChecklistTask.Create(userId, "Thiết kế thiệp cưới online & gửi link RSVP cho bạn bè", "1 - 2 Tháng"),
                ChecklistTask.Create(userId, "Thử váy cưới, vest chú rể lần cuối & chốt thực đơn tiệc", "1 - 2 Tháng"),
                ChecklistTask.Create(userId, "Chuẩn bị phong bao lì xì bưng quả & nghỉ ngơi thư giãn", "1 Tuần & Ngày Cưới")
            };

            _context.ChecklistTasks.AddRange(seedTasks);
            await _context.SaveChangesAsync(cancellationToken);
            tasks = seedTasks;
        }

        return tasks.Select(t => new ChecklistTaskDto(
            Id: t.Id,
            Title: t.Title,
            Milestone: t.Milestone,
            DueDate: t.DueDate,
            IsCompleted: t.IsCompleted,
            Notes: t.Notes,
            CreatedAt: t.CreatedAt
        )).ToList();
    }
}
