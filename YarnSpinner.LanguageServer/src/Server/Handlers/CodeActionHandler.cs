﻿using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YarnLanguageServer.Diagnostics;

namespace YarnLanguageServer.Handlers
{
    internal class CodeActionHandler : ICodeActionHandler
    {
        private Workspace workspace;

        public CodeActionHandler(Workspace workspace)
        {
            this.workspace = workspace;
        }

        public Task<CommandOrCodeActionContainer> Handle(CodeActionParams request, CancellationToken cancellationToken)
        {
            var results = new List<CommandOrCodeAction>();
            foreach (var diagnostic in request.Context.Diagnostics)
            {
                if (!diagnostic.Code.HasValue || !diagnostic.Code.Value.IsString)
                {
                    continue;
                }

                switch (diagnostic.Code.Value.String)
                {
                    case nameof(YarnDiagnosticCode.YRNMsngCmdDef):
                        results.AddRange(HandleYRNMsngCmdDef(diagnostic, request.TextDocument.Uri));
                        break;
                    case nameof(YarnDiagnosticCode.YRNMsngVarDec):
                        results.AddRange(HandleYRNMsngVarDec(diagnostic, request.TextDocument.Uri));
                        break;
                    case nameof(YarnDiagnosticCode.YRNMsngJumpDest):
                        results.AddRange(HandleYRNMsngJumpDest(diagnostic, request.TextDocument.Uri));
                        break;
                }
            }

            return Task.FromResult<CommandOrCodeActionContainer>(results);
        }

        public CodeActionRegistrationOptions GetRegistrationOptions(CodeActionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new CodeActionRegistrationOptions
            {
                DocumentSelector = Utils.YarnDocumentSelector,
                ResolveProvider = false,
                CodeActionKinds = new Container<CodeActionKind>(CodeActionKind.QuickFix),

                // TODO Consider implementing CodeActionKind.SourceFixAll available in proposed lsp 3.17
            };
        }

        private IEnumerable<CommandOrCodeAction> HandleYRNMsngVarDec(Diagnostic diagnostic, DocumentUri uri)
        {
            var name = diagnostic.Data?.ToString();
            if (string.IsNullOrEmpty(name)) { return Enumerable.Empty<CommandOrCodeAction>(); }

            // Suggest potential renamings by fuzzy-searching for the existing
            // input, and offering suggestions that replace the text.
            var suggestions =
                workspace.GetProjectsForUri(uri)
                .SelectMany(p => p.FindVariables(name, true))
                .Where(decl => decl.Name != name)
                .DistinctBy(d => d.Name)
                .Take(10)
                .Select(declaration =>
                {
                    var edits = new Dictionary<DocumentUri, IEnumerable<TextEdit>>();
                    edits[uri] = new List<TextEdit> { new TextEdit { NewText = declaration.Name, Range = diagnostic.Range } };

                    var replaceAction = new CodeAction
                    {
                        Title = $"Rename to '{declaration.Name}'",
                        Kind = CodeActionKind.QuickFix,
                        Edit = new WorkspaceEdit { Changes = edits },
                    };
                    return replaceAction;
                })
                .Select(s => new CommandOrCodeAction(s));

            var insertDeclarationEdit = new Dictionary<DocumentUri, IEnumerable<TextEdit>>();

            var existingDeclaration = workspace.GetProjectsForUri(uri)
                .SelectMany(p => p.FindVariables(name))
                .FirstOrDefault(decl => decl.IsImplicit);

            if (existingDeclaration != null)
            {
                // We have the implicit declaration of this variable, so we know
                // its type and its initial value. Create a declaration using
                // this.
                string type;
                string defaultValue;

                DeclarationHelper.GetDeclarationInfo(existingDeclaration, out type, out defaultValue);

                // TODO: possible to indent / space in line with other
                // statements?
                var declarationText = $"<<declare {name} = {defaultValue} as {type}>>\n";

                var insertPosition = new Position(diagnostic.Range.Start.Line, 0);

                insertDeclarationEdit[uri] = new List<TextEdit> {
                    new TextEdit {
                        NewText = declarationText,
                        Range = new Range(insertPosition, insertPosition),
                    },
                };

                suggestions = suggestions.Prepend(
                    new CommandOrCodeAction(
                        new CodeAction
                        {
                            Title = $"Generate variable declaration '{name}'",
                            Kind = CodeActionKind.QuickFix,
                            IsPreferred = true,
                            Edit = new WorkspaceEdit { Changes = insertDeclarationEdit },
                        }
                    )
                );
            }

            return suggestions;
        }

        private IEnumerable<CommandOrCodeAction> HandleYRNMsngCmdDef(Diagnostic diagnostic, DocumentUri uri)
        {
            if (diagnostic.Data?.HasValues != true)
            {
                return Enumerable.Empty<CommandOrCodeAction>();
            }

            var functionInfo = diagnostic.Data.ToObject<(string Name, bool IsCommand)>();
            var suggestions =
                workspace.GetProjectsForUri(uri)
                .SelectMany(p => p.FindActions(functionInfo.Name, ActionType.Command, true))
                .Take(10)
                .Select(f =>
                {
                    var edits = new Dictionary<DocumentUri, IEnumerable<TextEdit>>();
                    edits[uri] = new List<TextEdit> { new TextEdit { NewText = f.YarnName, Range = diagnostic.Range } };

                    var replaceAction = new CodeAction
                    {
                        Title = $"Rename to '{f.YarnName}'",
                        Kind = CodeActionKind.QuickFix,
                        Edit = new WorkspaceEdit { Changes = edits },
                    };
                    return replaceAction;
                })
                .Select(s => new CommandOrCodeAction(s));

            return suggestions;
        }

        private IEnumerable<CommandOrCodeAction> HandleYRNMsngJumpDest(Diagnostic diagnostic, DocumentUri uri)
        {
            var jumpDestination = diagnostic.Data?.ToString();
            if (string.IsNullOrEmpty(jumpDestination)) { return Enumerable.Empty<CommandOrCodeAction>(); }

            var project = workspace.GetProjectsForUri(uri).FirstOrDefault();
            if (project == null) { return Enumerable.Empty<CommandOrCodeAction>(); }

            // Suggest potential renamings by fuzzy-searching for the existing
            // input, and offering suggestions that replace the text.
            var suggestions =
                project.FindNodes(jumpDestination, true)
                .Where(name => name != jumpDestination)
                .Distinct()
                .Take(10)
                .Select(name =>
                {
                    var edits = new Dictionary<DocumentUri, IEnumerable<TextEdit>>();
                    edits[uri] = new List<TextEdit> { new TextEdit { NewText = name, Range = diagnostic.Range } };

                    var replaceAction = new CodeAction
                    {
                        Title = $"Rename to '{name}'",
                        Kind = CodeActionKind.QuickFix,
                        Edit = new WorkspaceEdit { Changes = edits },
                    };
                    return replaceAction;
                })
                .Select(s => new CommandOrCodeAction(s));

            var yarnFile = project.GetFileData((System.Uri)uri);
            if (yarnFile == null) { return suggestions; }

            // Offer to create a new node with the missing node title if the jump destination doesn't exist
            var insertDeclarationEdit = new Dictionary<DocumentUri, IEnumerable<TextEdit>>();

            // Could share most of this code with AddNodeToDocumentAsync command
            var newNodeText = new System.Text.StringBuilder()
                .AppendLine($"title: {jumpDestination}")
                .AppendLine("---")
                .AppendLine()
                .AppendLine("===");

            Position insertPosition;

            var lastLineIsEmpty = yarnFile.Text.EndsWith('\n');

            int lastLineIndex = yarnFile.LineCount - 1;

            if (lastLineIsEmpty)
            {
                // The final line ends with a newline. Insert the node
                // there.
                insertPosition = new Position(lastLineIndex, 0);
            }
            else
            {
                // The final line does not end with a newline. Insert a
                // newline at the end of the last line, followed by the new
                // text.
                var endOfLastLine = yarnFile.GetLineLength(lastLineIndex);
                newNodeText.Insert(0, System.Environment.NewLine);
                insertPosition = new Position(lastLineIndex, endOfLastLine);
            }

            insertDeclarationEdit[uri] = new List<TextEdit> {
                new TextEdit {
                    NewText = newNodeText.ToString(),
                    Range = new Range(insertPosition, insertPosition),
                },
            };

            suggestions = suggestions.Prepend(
                new CommandOrCodeAction(
                    new CodeAction
                    {
                        Title = $"Generate node '{jumpDestination}'",
                        Kind = CodeActionKind.QuickFix,
                        IsPreferred = true,
                        Edit = new WorkspaceEdit { Changes = insertDeclarationEdit },
                    }
                )
            );

            return suggestions;
        }
    }
}
