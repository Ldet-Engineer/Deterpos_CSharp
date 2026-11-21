using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.WPF.ViewModels;

public class ProductDisplayItem : ViewModelBase
{
    private decimal _bcvRate;

    public Product Entity { get; }

    public int Id => Entity.Id;
    public string Code => Entity.Code;
    public string Barcode => Entity.Barcode;
    public string Name => Entity.Name;
    public string Description => Entity.Description;
    public string CategoryName => Entity.CategoryName;
    public decimal CostUSD => Entity.CostUSD;
    public decimal PriceUSD => Entity.PriceUSD;
    public decimal CurrentStock => Entity.CurrentStock;
    public decimal MinimumStock => Entity.MinimumStock;
    public VatRateType VatRate => Entity.VatRate;
    public bool IsActive => Entity.IsActive;

    public decimal BcvRate
    {
        get => _bcvRate;
        set
        {
            if (SetProperty(ref _bcvRate, value))
            {
                OnPropertyChanged(nameof(CalculatedPriceVES));
            }
        }
    }

    public decimal CalculatedPriceVES => Entity.PriceUSD * _bcvRate;
    public bool IsLowStock => Entity.CurrentStock <= Entity.MinimumStock && Entity.CurrentStock > 0;
    public bool IsOutOfStock => Entity.CurrentStock <= 0;

    public ProductDisplayItem(Product product, decimal bcvRate)
    {
        Entity = product;
        _bcvRate = bcvRate;
    }
}
