using AuthDemo.DTOs;

namespace AuthDemo.Services.Interfaces;

public interface IAppointmentService
{
    // =====================================================
    // CREATE APPOINTMENT
    // =====================================================

    Task CreateAsync(
        BookSlotDto dto,
        int hospitalId
    );

    // =====================================================
    // GET ALL APPOINTMENTS
    // =====================================================

    Task<List<AppointmentResponseDto>>
        GetAllAsync(
            int hospitalId
        );

    // =====================================================
    // UPDATE STATUS
    // =====================================================

    Task<bool>
        UpdateStatusAsync(
            int id,
            string status,
            int hospitalId
        );
    //======//
    //method//
    //========//
    Task<List<AppointmentResponseDto>>
    GetByBranchAsync(
        int hospitalId,
        int branchId
    );
}