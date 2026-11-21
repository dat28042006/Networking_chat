namespace ChatApp.Client.UI.Controls;

/// <summary>A flat dark text box (a Panel hosting a borderless TextBox so it looks consistent).</summary>
public sealed class InputBox : Panel
{
    public TextBox Inner { get; } = new();

    public InputBox()
    {
        BackColor = Theme.Input;
        Padding = new Padding(12, 9, 12, 9);
        Height = 40;

        Inner.BorderStyle = BorderStyle.None;
        Inner.BackColor = Theme.Input;
        Inner.ForeColor = Theme.Text;
        Inner.Font = Theme.Regular;
        Inner.Dock = DockStyle.Fill;
        Controls.Add(Inner);

        Click += (_, _) => Inner.Focus();
    }

    public string Value { get => Inner.Text; set => Inner.Text = value; }
    public string PlaceholderText { get => Inner.PlaceholderText; set => Inner.PlaceholderText = value; }
    public int MaxLength { get => Inner.MaxLength; set => Inner.MaxLength = value; }
    public bool IsPassword { get => Inner.UseSystemPasswordChar; set => Inner.UseSystemPasswordChar = value; }
}
