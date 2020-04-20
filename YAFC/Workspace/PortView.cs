using Routing;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public abstract class PortView : IWidget
    {
        private readonly NodeView owner;
        private readonly GridPos offset;
        public Goods goods => nodePort.goods;
        public abstract NodePort nodePort { get; }
        
        
        public PortView(NodeView owner, GridPos offset)
        {
            this.owner = owner;
            this.offset = offset;
        }
        public void Build(LayoutState state)
        {
            
        }
    }

    public class IngredientView : PortView
    {
        public readonly RecipeIngredient ingredient;
        
        public IngredientView(NodeView owner, RecipeIngredient ingredient, GridPos offset) : base(owner, offset)
        {
            this.ingredient = ingredient;
        }

        public override NodePort nodePort => ingredient;
    }

    public class ProductView : PortView
    {
        public readonly RecipeProduct product;
        
        public ProductView(NodeView owner, RecipeProduct product, GridPos offset) : base(owner, offset)
        {
            this.product = product;
        }

        public override NodePort nodePort => product;
    }
}