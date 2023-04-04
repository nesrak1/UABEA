using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

namespace UABEAvalonia
{
    public partial class MessageBox : Window
    {
        private MessageBoxType type;

        public MessageBox()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            btn1.Click += Btn1_Click;
            btn2.Click += Btn2_Click;
            btn3.Click += Btn3_Click;
        }

        public MessageBox(string title, string message, MessageBoxType type) : this()
        {
            this.type = type;
            titleLbl.Content = title;
            msgTextBox.Text = message;
            
            if (type == MessageBoxType.OK)
            {
                stackPanel.Children.Remove(btn2);
                stackPanel.Children.Remove(btn3);
                btn1.Content = "OK";
            }
            else if (type == MessageBoxType.OKCancel)
            {
                stackPanel.Children.Remove(btn3);
                btn1.Content = "OK";
                btn2.Content = "Cancel";
            }
            else if (type == MessageBoxType.YesNo)
            {
                stackPanel.Children.Remove(btn3);
            }
            else if (type == MessageBoxType.Custom)
            {
                throw new Exception("use the other constructor for custom button texts");
            }
        }

        public MessageBox(string title, string message, MessageBoxType type, string[] buttonTexts) : this()
        {
            this.type = type;
            titleLbl.Content = title;
            msgTextBox.Text = message;

            if (type != MessageBoxType.Custom)
            {
                throw new Exception("use the other constructor for non-custom button texts");
            }

            if (buttonTexts.Length < 1 || buttonTexts.Length > 3)
            {
                throw new ArgumentException("buttonTexts length was not between 1 and 3.");
            }

            if (buttonTexts.Length == 1)
            {
                stackPanel.Children.Remove(btn2);
                stackPanel.Children.Remove(btn3);
                btn1.Content = buttonTexts[0];
            }
            else if (buttonTexts.Length == 2)
            {
                stackPanel.Children.Remove(btn3);
                btn1.Content = buttonTexts[0];
                btn2.Content = buttonTexts[1];
            }
            else
            {
                btn1.Content = buttonTexts[0];
                btn2.Content = buttonTexts[1];
                btn3.Content = buttonTexts[2];
            }
        }

        private void Btn1_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (type == MessageBoxType.OK)
            {
                Close(MessageBoxResult.OK);
            }
            else if (type == MessageBoxType.OKCancel)
            {
                Close(MessageBoxResult.OK);
            }
            else if (type == MessageBoxType.YesNo)
            {
                Close(MessageBoxResult.Yes);
            }
            else if (type == MessageBoxType.YesNoCancel)
            {
                Close(MessageBoxResult.Yes);
            }
            else if (type == MessageBoxType.Custom)
            {
                Close(MessageBoxResult.CustomButtonA);
            }
            Close(MessageBoxResult.Unknown);
        }

        private void Btn2_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (type == MessageBoxType.OKCancel)
            {
                Close(MessageBoxResult.Cancel);
            }
            else if (type == MessageBoxType.YesNo)
            {
                Close(MessageBoxResult.No);
            }
            else if (type == MessageBoxType.YesNoCancel)
            {
                Close(MessageBoxResult.No);
            }
            else if (type == MessageBoxType.Custom)
            {
                Close(MessageBoxResult.CustomButtonB);
            }
            Close(MessageBoxResult.Unknown);
        }

        private void Btn3_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (type == MessageBoxType.YesNoCancel)
            {
                Close(MessageBoxResult.Cancel);
            }
            else if (type == MessageBoxType.Custom)
            {
                Close(MessageBoxResult.CustomButtonC);
            }
            Close(MessageBoxResult.Unknown);
        }
    }

    public enum MessageBoxType
    {
        OK,
        OKCancel,
        YesNo,
        YesNoCancel,
        Custom
    }

    public enum MessageBoxResult
    {
        Unknown,
        OK,
        Yes,
        No,
        Cancel,
        CustomButtonA,
        CustomButtonB,
        CustomButtonC
    }
}
