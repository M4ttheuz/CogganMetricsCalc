using System.ComponentModel.DataAnnotations;

namespace inzynierka.Models.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Adres e-mail jest wymagany")]
    [EmailAddress(ErrorMessage = "Niepoprawny format adresu e-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Hasło jest wymagane")]
    [MinLength(6, ErrorMessage = "Hasło musi mieć minimum 6 znaków")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Potwierdzenie hasła jest wymagane")]
    [Compare("Password", ErrorMessage = "Hasła nie są identyczne")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj aktualną masę ciała")]
    [Range(35, 200, ErrorMessage = "Waga musi mieścić się w przedziale 35 - 200 kg")]
    public decimal WeightKg { get; set; }

    [Required(ErrorMessage = "Podaj aktualne FTP")]
    [Range(50, 600, ErrorMessage = "FTP musi mieścić się w przedziale 50 - 600 W")]
    public int InitialFtp { get; set; }
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Podaj adres e-mail")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj hasło")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}