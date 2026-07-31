using AuthDemo.DTOs;

namespace AuthDemo.Services.Interfaces;


public interface IPaymentService
{

    Task<int> CreatePaymentAsync(
        CreatePaymentDto dto,
        int hospitalId
    );


    Task<bool> CompletePaymentAsync(
        PaymentSuccessDto dto,
        int hospitalId
    );

}