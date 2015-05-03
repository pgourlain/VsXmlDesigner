using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace Genius.VisualStudio.BaseEditors
{
    static class ServicesExtensions
    {

        public static Microsoft.XmlEditor.XmlLanguageService XmlLanguageService(this IServiceProvider spp)
        {
            return (Microsoft.XmlEditor.XmlLanguageService)spp.GetService(typeof(Microsoft.XmlEditor.XmlLanguageService));
        }
        public static Microsoft.XmlEditor.XmlLanguageService XmlLanguageService(this Microsoft.VisualStudio.Shell.WindowPane window)
        {
            return XmlLanguageService((IServiceProvider)window);
        }

        /// <summary>
        /// Helper function used to add commands using IMenuCommandService
        /// </summary>
        /// <param name="mcs"> The IMenuCommandService interface.</param>
        /// <param name="menuGroup"> This guid represents the menu group of the command.</param>
        /// <param name="cmdID"> The command ID of the command.</param>
        /// <param name="commandEvent"> An EventHandler which will be called whenever the command is invoked.</param>
        /// <param name="queryEvent"> An EventHandler which will be called whenever we want to query the status of
        /// the command.  If null is passed in here then no EventHandler will be added.</param>
        public static void AddCommand(this IMenuCommandService mcs, Guid menuGroup, int cmdID,
                                       EventHandler commandEvent, EventHandler queryEvent)
        {
            // Create the OleMenuCommand from the menu group, command ID, and command event
            CommandID menuCommandID = new CommandID(menuGroup, cmdID);
            OleMenuCommand command = new OleMenuCommand(commandEvent, menuCommandID);

            // Add an event handler to BeforeQueryStatus if one was passed in
            if (null != queryEvent)
            {
                command.BeforeQueryStatus += queryEvent;
            }

            // Add the command using our IMenuCommandService instance
            mcs.AddCommand(command);
        }

    }
}
