using Avalonia.Controls;
//using MessageBox.Avalonia;
//using MessageBox.Avalonia.DTO;
//using MessageBox.Avalonia.Enums;
//using MessageBox.Avalonia.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public static class MessageBoxUtil
    {
        public static async Task<MessageBoxResult> ShowDialog(Window window, string header, string message)
        {
            MessageBox mb = new MessageBox(header, message, MessageBoxType.OK);
            return await mb.ShowDialog<MessageBoxResult>(window);
        }

        public static async Task<MessageBoxResult> ShowDialog(Window window, string header, string message, MessageBoxType buttons)
        {
            MessageBox mb = new MessageBox(header, message, buttons);
            return await mb.ShowDialog<MessageBoxResult>(window);
        }

        public static async Task<string> ShowDialogCustom(Window window, string header, string message, params string[] buttons)
        {
            MessageBox mb = new MessageBox(header, message, MessageBoxType.Custom, buttons);
            MessageBoxResult res = await mb.ShowDialog<MessageBoxResult>(window);
            if (res == MessageBoxResult.CustomButtonA)
                return buttons[0];
            else if (res == MessageBoxResult.CustomButtonB)
                return buttons[1];
            else if (res == MessageBoxResult.CustomButtonC)
                return buttons[2];

            return string.Empty;
        }
    }
}
