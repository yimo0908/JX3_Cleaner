using System.Windows;
using System.Windows.Controls;

namespace JX3Cleaner.App.Controls;

/// <summary>
/// 加减框：只允许点「−」「+」调整数值，不提供可键入的输入框，
/// 因此不存在「用户乱输导致解析失败」的分支。
/// </summary>
public partial class NumberStepper : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(int), typeof(NumberStepper),
        new FrameworkPropertyMetadata(0,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceValue));

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
        nameof(Step), typeof(int), typeof(NumberStepper), new PropertyMetadata(1));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(int), typeof(NumberStepper), new PropertyMetadata(0, OnValueChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(int), typeof(NumberStepper), new PropertyMetadata(999, OnValueChanged));

    public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(
        nameof(Suffix), typeof(string), typeof(NumberStepper), new PropertyMetadata("个", OnSuffixChanged));

    public NumberStepper()
    {
        InitializeComponent();
        Refresh();
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>每次点击的步长。时间类 5 天，条数类 1 个。</summary>
    public int Step
    {
        get => (int)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public int Minimum
    {
        get => (int)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>紧跟数字后的单位文字，如「个」「天」「组」。</summary>
    public string Suffix
    {
        get => (string)GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        var stepper = (NumberStepper)d;
        var value = (int)baseValue;
        return Math.Clamp(value, stepper.Minimum, stepper.Maximum);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((NumberStepper)d).Refresh();

    private static void OnSuffixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var stepper = (NumberStepper)d;
        stepper.SuffixText.Text = e.NewValue as string ?? string.Empty;
    }

    private void Minus_Click(object sender, RoutedEventArgs e) => Value -= StepSize;

    private void Plus_Click(object sender, RoutedEventArgs e) => Value += StepSize;

    private int StepSize => Math.Max(1, Math.Abs(Step));

    private void Refresh()
    {
        if (ValueText is null) return;   // 构造期间 InitializeComponent 之前可能触发

        ValueText.Text = Value.ToString();
        SuffixText.Text = Suffix;

        MinusButton.IsEnabled = Value > Minimum;
        PlusButton.IsEnabled = Value < Maximum;
    }
}
