using Avalonia.Controls;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using MessageBox.Avalonia.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public static class MessageBoxUtil
    {
        public static async Task<ButtonResult> ShowDialog(Window window, string header, string message)
        {
            return await ShowDialog(window, header, message, ButtonEnum.Ok);
        }

        public static async Task<ButtonResult> ShowDialog(Window window, string header, string message, ButtonEnum buttons)
        {
            return await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
            {
                ButtonDefinitions = buttons,
                ContentHeader = header,
                ContentMessage = message
            }).ShowDialog(window);
        }

        public static async Task<string> ShowDialogCustom(Window window, string header, string message, params string[] buttons)
        {
            ButtonDefinition[] definitions = new ButtonDefinition[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                definitions[i] = new ButtonDefinition { Name = buttons[i], IsDefault = true };
            }

            return await MessageBoxManager.GetMessageBoxCustomWindow(new MessageBoxCustomParams
            {
                ContentHeader = header,
                ContentMessage = message,
                ButtonDefinitions = definitions
            }).ShowDialog(window);
        }
    }
}
