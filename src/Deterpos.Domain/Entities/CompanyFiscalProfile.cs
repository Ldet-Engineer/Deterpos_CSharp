using Deterpos.Domain.Enums;

namespace Deterpos.Domain.Entities;

public class CompanyFiscalProfile
{
    public int Id { get; set; }
    public string BusinessName { get; set; } = "Deterpos C.A."; // Razón Social
    public string TradeName { get; set; } = "Deterpos"; // Nombre Comercial
    public string RifPrefix { get; set; } = "J"; // V, E, J, G, P
    public string RifNumber { get; set; } = "409182730"; // Número sin guiones
    public string FiscalAddress { get; set; } = "Av. Principal, Edificio Empresarial, Piso 1, Caracas, Venezuela";
    public string Phone { get; set; } = "0212-5551234";
    public string Email { get; set; } = "contacto@deterpos.com";
    
    // Configuración Contribuyente Especial (IGTF 3%)
    public bool IsSpecialTaxpayer { get; set; } = true; // Sujeto Pasivo Especial
    public decimal IgtfPercentage { get; set; } = 3.00m; // 3%
    
    // Configuración Hardware Fiscal
    public FiscalPrinterBrand PrinterBrand { get; set; } = FiscalPrinterBrand.TheFactoryHKA;
    public string PortName { get; set; } = "COM1"; // COM1, COM2, o IP:Puerto
    public int BaudRate { get; set; } = 9600;
    
    // Configuración Forma Libre (Si no usa impresora fiscal física)
    public string FreeFormControlPrefix { get; set; } = "00-";
    public long FreeFormCurrentControlNumber { get; set; } = 1;
}
