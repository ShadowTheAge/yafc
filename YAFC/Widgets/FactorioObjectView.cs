using System;
using System.Numerics;
using System.Text;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class FactorioObjectIconView : IWidget, IMouseHandle, IListView<FactorioObject>
    {
        protected IFactorioObjectWrapper _target;
        private UiBatch batch;
        protected bool accessible;
        public virtual void Build(LayoutState state)
        {
            batch = state.batch;
            var tgt = _target?.target;
            if (tgt == null)
                return;
            state.BuildIcon(tgt.icon, SchemeColor.Source, 2f);
            var milestone = Milestones.GetHighest(tgt);
            if (milestone != null)
            {
                var rect = new Rect(state.lastRect.BottomRight - Vector2.One, Vector2.One);
                state.batch.DrawIcon(rect, milestone.icon, accessible ? SchemeColor.Source : SchemeColor.SourceTransparent);
            }
        }

        public IFactorioObjectWrapper target
        {
            get => _target;
            set
            {
                if (_target == value)
                    return;
                _target = value;
                accessible = value.target?.IsAccessible() ?? false;
                UpdateTarget(value);
                batch?.Rebuild();
            }
        }

        protected virtual void UpdateTarget(IFactorioObjectWrapper target) {}

        public void MouseEnter(HitTestResult<IMouseHandle> hitTest)
        {
            MainScreen.Instance.ShowTooltip(target.target, hitTest);
        }

        public void MouseExit(UiBatch batch) {}

        public void MouseClick(int button, UiBatch batch)
        {
            ;
        }

        public void BuildElement(FactorioObject element, LayoutState state)
        {
            target = element;
            Build(state);
        }
    }

    public class FactorioObjectView : FactorioObjectIconView
    {
        public readonly bool showObjectType;
        public FactorioObjectView(bool showObjectType)
        {
            this.showObjectType = showObjectType;
        }
        private readonly FontString text = new FontString(Font.text);
        public override void Build(LayoutState state)
        {
            using (state.EnterRow())
            {
                base.Build(state);
                text.Build(state);
            }
        }

        protected override void UpdateTarget(IFactorioObjectWrapper target)
        {
            base.UpdateTarget(target);
            var txt = target?.text ?? "";
            if (showObjectType && target != null)
            {
                txt += " (";
                if (!target.target.IsAccessible())
                    txt += "Inaccessible ";
                txt += target.GetType().Name + ")";
            }

            text.text = txt;
            text.SetTransparent(!accessible);
        }

        private const char no = (char) 0;
        private static readonly (char suffix, float multiplier, float dec)[] FormatSpec =
        {
            ('μ', 1e8f,  100f),
            ('μ', 1e8f,  100f),
            ('μ', 1e7f,  10f),
            ('μ', 1e6f,  1f),
            ('μ', 1e6f,  1f), // skipping m (milli-) because too similar to M (mega-)
            (no,  1e4f,  10000f),
            (no,  1e3f,  1000f),
            (no,  1e2f,  100f),
            (no,  1e1f,  10f), // [1-10]
            (no,  1e0f,  1f), 
            (no,  1e0f,  1f),
            ('K', 1e-2f, 10f),
            ('K', 1e-3f, 1f),
            ('M', 1e-4f, 100f),
            ('M', 1e-5f, 10f),
            ('M', 1e-6f, 1f),
            ('G', 1e-7f, 100f),
            ('G', 1e-8f, 10f),
            ('G', 1e-9f, 1f),
            ('T', 1e-10f, 100f),
            ('T', 1e-11f, 10f),
            ('T', 1e-12f, 1f),
        };

        private static readonly StringBuilder amountBuilder = new StringBuilder();
        public static string FormatAmount(float amount, bool isPower = false)
        {
            if (amount <= 0)
                return "0";
            amountBuilder.Clear();
            if (amount < 0)
            {
                amountBuilder.Append('-');
                amount = -amount;
            }
            if (isPower)
                amount *= 1e6f;
            var idx = MathUtils.Clamp(MathUtils.Floor(MathF.Log10(amount)) + 8, 0, FormatSpec.Length-1);
            var val = FormatSpec[idx];
            amountBuilder.Append(MathUtils.Round(amount * val.multiplier) / val.dec);
            if (val.suffix != no)
                amountBuilder.Append(val.suffix);
            if (isPower)
                amountBuilder.Append("W");
            return amountBuilder.ToString();
        }
    }
}