using System;

namespace PerfumeStore.DesignPatterns.Decorator
{

    public interface IProduct
    {
        string GetDescription();
        decimal GetPrice();
    }


    public class BasePerfume : IProduct
    {
        private string _name;
        private decimal _price;

        public BasePerfume(string name, decimal price)
        {
            _name = name;
            _price = price;
        }

        public string GetDescription() => _name;
        public decimal GetPrice() => _price;
    }
    public abstract class ProductDecorator : IProduct
    {
        protected IProduct _product;

        public ProductDecorator(IProduct product)
        {
            _product = product;
        }

        public virtual string GetDescription() => _product.GetDescription();
        public virtual decimal GetPrice() => _product.GetPrice();
    }


    public class GiftWrapDecorator : ProductDecorator
    {
        public GiftWrapDecorator(IProduct product) : base(product) { }

        public override string GetDescription()
        {
            return base.GetDescription() + " (+ Gói quà cao cấp)";
        }

        public override decimal GetPrice()
        {
            return base.GetPrice() + 50000; 
        }
    }

    public class EngraveNameDecorator : ProductDecorator
    {
        public EngraveNameDecorator(IProduct product) : base(product) { }

        public override string GetDescription()
        {
            return base.GetDescription() + " (+ Khắc tên theo yêu cầu)";
        }

        public override decimal GetPrice()
        {
            return base.GetPrice() + 100000; 
        }
    }
}