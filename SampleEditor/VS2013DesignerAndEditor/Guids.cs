// Guids.cs
// MUST match guids.h
using System;

namespace Genius.VS2013DesignerAndEditor
{
    static class GuidList
    {
        public const string guidVS2013DesignerAndEditorPkgString = "d1e04885-a52f-4b8a-be4f-5546b6bfb368";
        public const string guidVS2013DesignerAndEditorCmdSetString = "4d2d1acc-319e-41cd-9fda-0afe889c94e8";
        public const string guidVS2013DesignerAndEditorEditorFactoryString = "905dab8f-a0d2-4290-a5ba-bee4ffa5acfa";

        public static readonly Guid guidVS2013DesignerAndEditorCmdSet = new Guid(guidVS2013DesignerAndEditorCmdSetString);
        public static readonly Guid guidVS2013DesignerAndEditorEditorFactory = new Guid(guidVS2013DesignerAndEditorEditorFactoryString);
    };
}