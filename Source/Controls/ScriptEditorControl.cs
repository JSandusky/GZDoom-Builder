
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Drawing.Drawing2D;
using CodeImp.DoomBuilder.Config;
using System.IO;
using System.Reflection;
using CodeImp.DoomBuilder.IO;
using System.Collections;
using System.Globalization;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Properties;

#endregion

namespace CodeImp.DoomBuilder.Controls
{
	internal partial class ScriptEditorControl : UserControl
	{
		#region ================== Constants
		
		private const string LEXERS_RESOURCE = "Lexers.cfg";
		private const int DEFAULT_STYLE = (int)ScriptStylesCommon.Default;
		private const int MAX_BACKTRACK_LENGTH = 200;

		// Index for registered images
		private enum ImageIndex : int
		{
			ScriptConstant = 0,
			ScriptKeyword = 1,
			ScriptError = 2
		}
		
		#endregion

		#region ================== Delegates / Events

		#endregion

		#region ================== Properties
		
		public string Text { get { return scriptedit.Text; } set { scriptedit.Text = value; } }
		public bool IsChanged { get { return scriptedit.CanUndo; } }
		
		#endregion

		#region ================== Variables
		
		// Script configuration
		private ScriptConfiguration scriptconfig;
		
		// List of keywords and constants, sorted as uppercase
		private string autocompletestring;

		// Style translation from Scintilla style to ScriptStyleType
		private Dictionary<int, ScriptStyleType> stylelookup;
		
		// Current position information
		private string curfunctionname = "";
		private int curargumentindex = 0;
		private int curfunctionstartpos = 0;
		
		#endregion

		#region ================== Contructor / Disposer

		// Constructor
		public ScriptEditorControl()
		{
			// Initialize
			InitializeComponent();
			
			// Script editor properties
			// Unfortunately, these cannot be set using the designer
			// because the control is not really loaded in design mode
			scriptedit.AutoCMaximumHeight = 8;
			scriptedit.AutoCSeparator = ' ';
			scriptedit.AutoCTypeSeparator = '?';
			scriptedit.CaretWidth = 2;
			scriptedit.EndAtLastLine = 1;
			scriptedit.EndOfLineMode = ScriptEndOfLine.CRLF;
			scriptedit.IsAutoCGetChooseSingle = true;
			scriptedit.IsAutoCGetIgnoreCase = true;
			scriptedit.IsBackSpaceUnIndents = true;
			scriptedit.IsBufferedDraw = true;
			scriptedit.IsCaretLineVisible = false;
			scriptedit.IsHScrollBar = true;
			scriptedit.IsIndentationGuides = true;
			scriptedit.IsMouseDownCaptures = true;
			scriptedit.IsTabIndents = true;
			scriptedit.IsUndoCollection = true;
			scriptedit.IsUseTabs = true;
			scriptedit.IsViewEOL = false;
			scriptedit.IsVScrollBar = true;
			scriptedit.SetFoldFlags((int)ScriptFoldFlag.Box);
			
			// Symbol margin
			scriptedit.SetMarginTypeN(0, (int)ScriptMarginType.Symbol);
			scriptedit.SetMarginWidthN(0, 20);
			scriptedit.SetMarginMaskN(0, -1);	// all
			
			// Line numbers margin
			scriptedit.SetMarginTypeN(1, (int)ScriptMarginType.Number);
			scriptedit.SetMarginWidthN(1, 40);
			scriptedit.SetMarginMaskN(1, 0);	// none

			// Spacing margin
			scriptedit.SetMarginTypeN(2, (int)ScriptMarginType.Symbol);
			scriptedit.SetMarginWidthN(2, 5);
			scriptedit.SetMarginMaskN(2, 0);	// none
			
			// Setup with default script config
			// Disabled, the form designer doesn't like this
			//SetupStyles(new ScriptConfiguration());

			// Images
			RegisterAutoCompleteImage(ImageIndex.ScriptConstant, Resources.ScriptConstant);
			RegisterAutoCompleteImage(ImageIndex.ScriptKeyword, Resources.ScriptKeyword);
			RegisterMarkerImage(ImageIndex.ScriptError, Resources.ScriptError);
		}
		
		#endregion
		
		#region ================== Methods
		
		// This sets up the script editor with a script configuration
		public void SetupStyles(ScriptConfiguration config)
		{
			Stream lexersdata;
			StreamReader lexersreader;
			Configuration lexercfg = new Configuration();
			List<string> autocompletelist = new List<string>();
			string[] resnames;
			int imageindex;
			
			// Make collections
			stylelookup = new Dictionary<int, ScriptStyleType>();
			
			// Keep script configuration
			scriptconfig = config;
			
			// Find a resource named Lexers.cfg
			resnames = General.ThisAssembly.GetManifestResourceNames();
			foreach(string rn in resnames)
			{
				// Found one?
				if(rn.EndsWith(LEXERS_RESOURCE, StringComparison.InvariantCultureIgnoreCase))
				{
					// Get a stream from the resource
					lexersdata = General.ThisAssembly.GetManifestResourceStream(rn);
					lexersreader = new StreamReader(lexersdata, Encoding.ASCII);

					// Load configuration from stream
					lexercfg.InputConfiguration(lexersreader.ReadToEnd());

					// Done with the resource
					lexersreader.Dispose();
					lexersdata.Dispose();
				}
			}
			
			// Check if specified lexer exists and set the lexer to use
			string lexername = "lexer" + scriptconfig.Lexer.ToString(CultureInfo.InvariantCulture);
			if(!lexercfg.SettingExists(lexername)) throw new InvalidOperationException("Unknown lexer " + scriptconfig.Lexer + " specified in script configuration!");
			scriptedit.Lexer = scriptconfig.Lexer;
			
			// Set the default style and settings
			scriptedit.StyleSetFont(DEFAULT_STYLE, "Lucida Console");
			scriptedit.StyleSetSize(DEFAULT_STYLE, 10);
			scriptedit.StyleSetBold(DEFAULT_STYLE, false);
			scriptedit.StyleSetItalic(DEFAULT_STYLE, false);
			scriptedit.StyleSetUnderline(DEFAULT_STYLE, false);
			scriptedit.StyleSetCase(DEFAULT_STYLE, ScriptCaseVisible.Mixed);
			scriptedit.StyleSetFore(DEFAULT_STYLE, General.Colors.PlainText.ToColorRef());
			scriptedit.StyleSetBack(DEFAULT_STYLE, General.Colors.ScriptBackground.ToColorRef());
			scriptedit.CaretPeriod = SystemInformation.CaretBlinkTime;
			scriptedit.CaretFore = General.Colors.ScriptBackground.Inverse().ToColorRef();
			scriptedit.StyleBits = 7;
			
			// This applies the default style to all styles
			scriptedit.StyleClearAll();
			
			// Set the default to something normal (this is used by the autocomplete list)
			scriptedit.StyleSetFont(DEFAULT_STYLE, this.Font.Name);
			scriptedit.StyleSetBold(DEFAULT_STYLE, this.Font.Bold);
			scriptedit.StyleSetItalic(DEFAULT_STYLE, this.Font.Italic);
			scriptedit.StyleSetUnderline(DEFAULT_STYLE, this.Font.Underline);
			scriptedit.StyleSetSize(DEFAULT_STYLE, (int)Math.Round(this.Font.SizeInPoints));
			
			// Set style for linenumbers and margins
			scriptedit.StyleSetBack((int)ScriptStylesCommon.LineNumber, General.Colors.ScriptBackground.ToColorRef());
			
			// Clear all keywords
			for(int i = 0; i < 9; i++) scriptedit.KeyWords(i, null);
			
			// Now go for all elements in the lexer configuration
			// We are looking for the numeric keys, because these are the
			// style index to set and the value is our ScriptStyleType
			IDictionary dic = lexercfg.ReadSetting(lexername, new Hashtable());
			foreach(DictionaryEntry de in dic)
			{
				// Check if this is a numeric key
				int stylenum = -1;
				if(int.TryParse(de.Key.ToString(), out stylenum))
				{
					// Add style to lookup table
					stylelookup.Add(stylenum, (ScriptStyleType)(int)de.Value);
					
					// Apply color to style
					int colorindex = 0;
					switch((ScriptStyleType)(int)de.Value)
					{
						case ScriptStyleType.PlainText: colorindex = ColorCollection.PLAINTEXT; break;
						case ScriptStyleType.Comment: colorindex = ColorCollection.COMMENTS; break;
						case ScriptStyleType.Constant: colorindex = ColorCollection.CONSTANTS; break;
						case ScriptStyleType.Keyword: colorindex = ColorCollection.KEYWORDS; break;
						case ScriptStyleType.LineNumber: colorindex = ColorCollection.LINENUMBERS; break;
						case ScriptStyleType.Literal: colorindex = ColorCollection.LITERALS; break;
						default: colorindex = ColorCollection.PLAINTEXT; break;
					}
					scriptedit.StyleSetFore(stylenum, General.Colors.Colors[colorindex].ToColorRef());
				}
			}
			
			// Create the keywords list and apply it
			imageindex = (int)ImageIndex.ScriptKeyword;
			int keywordsindex = lexercfg.ReadSetting(lexername + ".keywordsindex", -1);
			if(keywordsindex > -1)
			{
				StringBuilder keywordslist = new StringBuilder("");
				foreach(string k in scriptconfig.Keywords)
				{
					if(keywordslist.Length > 0) keywordslist.Append(" ");
					keywordslist.Append(k);
					autocompletelist.Add(k + "?" + imageindex.ToString(CultureInfo.InvariantCulture));
				}
				string words = keywordslist.ToString();
				scriptedit.KeyWords(keywordsindex, words.ToLowerInvariant());
			}

			// Create the constants list and apply it
			imageindex = (int)ImageIndex.ScriptConstant;
			int constantsindex = lexercfg.ReadSetting(lexername + ".constantsindex", -1);
			if(constantsindex > -1)
			{
				StringBuilder constantslist = new StringBuilder("");
				foreach(string c in scriptconfig.Constants)
				{
					if(constantslist.Length > 0) constantslist.Append(" ");
					constantslist.Append(c);
					autocompletelist.Add(c + "?" + imageindex.ToString(CultureInfo.InvariantCulture));
				}
				string words = constantslist.ToString();
				scriptedit.KeyWords(constantsindex, words.ToLowerInvariant());
			}
			
			// Sort the autocomplete list
			autocompletelist.Sort(StringComparer.CurrentCultureIgnoreCase);
			autocompletestring = string.Join(" ", autocompletelist.ToArray());

			// Show/hide the functions bar
			functionbar.Visible = (scriptconfig.FunctionRegEx.Length > 0);

			// Rearrange the layout
			scriptedit.ClearDocumentStyle();
			scriptedit.Text = scriptedit.Text;
			this.PerformLayout();
		}
		
		
		// This returns the current word (where the caret is at)
		public string GetCurrentWord()
		{
			int wordstart = scriptedit.WordStartPosition(scriptedit.CurrentPos, true);
			int wordend = scriptedit.WordEndPosition(scriptedit.CurrentPos, true);

			if(wordstart > wordend)
				return scriptedit.Text.Substring(wordstart, wordend - wordstart);
			else
				return "";
		}
		
		
		// This returns the ScriptStyleType for a given Scintilla style
		private ScriptStyleType GetScriptStyle(int scintillastyle)
		{
			if(stylelookup.ContainsKey(scintillastyle))
				return stylelookup[scintillastyle];
			else
				return ScriptStyleType.PlainText;
		}
		
		
		// This gathers information about the current caret position
		private void UpdatePositionInfo()
		{
			int bracketlevel = 0;			// bracket level counting
			int argindex = 0;				// function argument counting
			int limitpos;					// lowest position we'll backtrack to
			int pos = scriptedit.CurrentPos;
			
			// Reset position info
			curfunctionname = "";
			curargumentindex = 0;
			curfunctionstartpos = 0;
			
			// Determine lowest backtrack position
			limitpos = scriptedit.CurrentPos - MAX_BACKTRACK_LENGTH;
			if(limitpos < 0) limitpos = 0;
			
			// We can only do this when we have function syntax information
			if((scriptconfig.ArgumentDelimiter.Length == 0) || (scriptconfig.FunctionClose.Length == 0) ||
			   (scriptconfig.FunctionOpen.Length == 0) || (scriptconfig.Terminator.Length == 0)) return;
			
			// Get int versions of the function syntax informantion
			int argumentdelimiter = scriptconfig.ArgumentDelimiter[0];
			int functionclose = scriptconfig.FunctionClose[0];
			int functionopen = scriptconfig.FunctionOpen[0];
			int terminator = scriptconfig.Terminator[0];
			
			// Continue backtracking until we reached the limitpos
			while(pos >= limitpos)
			{
				// Backtrack 1 character
				pos--;
				
				// Get the style and character at this position
				ScriptStyleType curstyle = GetScriptStyle(scriptedit.StyleAt(pos));
				int curchar = scriptedit.CharAt(pos);
				
				// Then meeting ) then increase bracket level
				// When meeting ( then decrease bracket level
				// When bracket level goes -1, then the next word should be the function name
				// Only when at bracket level 0, count the comma's for argument index
				
				// TODO:
				// Original code checked for scope character here and breaks if found
				
				// Check if in plain text or keyword
				if((curstyle == ScriptStyleType.PlainText) || (curstyle == ScriptStyleType.Keyword))
				{
					// Closing bracket
					if(curchar == functionclose)
					{
						bracketlevel++;
					}
					// Opening bracket
					else if(curchar == functionopen)
					{
						bracketlevel--;
						
						// Out of the brackets?
						if(bracketlevel < 0)
						{
							// Skip any whitespace before this bracket
							do
							{
								// Backtrack 1 character
								curchar = scriptedit.CharAt(--pos);
							}
							while((pos >= limitpos) && ((curchar == ' ') || (curchar == '\t') ||
														(curchar == '\r') || (curchar == '\n')));
							
							// NOTE: We may need to set onlyWordCharacters argument in the
							// following calls to false to get any argument delimiter included,
							// but this may also cause a valid keyword to be combined with other
							// surrounding characters that do not belong to the keyword.
							
							// Find the word before this bracket
							int wordstart = scriptedit.WordStartPosition(pos, true);
							int wordend = scriptedit.WordEndPosition(pos, true);
							string word = scriptedit.Text.Substring(wordstart, wordend - wordstart);
							if(word.Length > 0)
							{
								// Check if this is an argument delimiter
								// I can't remember why I did this, but I'll probably stumble
								// upon the problem if this doesn't work right (see note above)
								if(word[0] == argumentdelimiter)
								{
									// We are now in the parent function
									bracketlevel++;
									argindex = 0;
								}
								// Now check if this is a keyword
								else if(scriptconfig.IsKeyword(word))
								{
									// Found it!
									curfunctionname = scriptconfig.GetKeywordCase(word);
									curargumentindex = argindex;
									curfunctionstartpos = wordstart;
									break;
								}
								else
								{
									// Don't know this word
									break;
								}
							}
						}
					}
					// Argument delimiter
					else if(curchar == argumentdelimiter)
					{
						// Only count these at brackt level 0
						if(bracketlevel == 0) argindex++;
					}
					// Terminator
					else if(curchar == terminator)
					{
						// Can't find anything, break now
						break;
					}
				}
			}
		}
		
		// This clears all undo levels
		public void ClearUndoRedo()
		{
			scriptedit.EmptyUndoBuffer();
		}
		
		// This registers an XPM image for the autocomplete list
		private unsafe void RegisterAutoCompleteImage(ImageIndex index, byte[] imagedata)
		{
			// Convert to string
			string bigstring = Encoding.UTF8.GetString(imagedata);
			
			// Register image
			scriptedit.RegisterImage((int)index, bigstring);
		}

		// This registers an XPM image for the markes list
		private unsafe void RegisterMarkerImage(ImageIndex index, byte[] imagedata)
		{
			// Convert to string
			string bigstring = Encoding.UTF8.GetString(imagedata);

			// Register image
			scriptedit.MarkerDefinePixmap((int)index, bigstring);
		}

		// Perform undo
		public void Undo()
		{
			scriptedit.Undo();
		}

		// Perform redo
		public void Redo()
		{
			scriptedit.Redo();
		}

		// Perform cut
		public void Cut()
		{
			scriptedit.Cut();
		}

		// Perform copy
		public void Copy()
		{
			scriptedit.Copy();
		}

		// Perform paste
		public void Paste()
		{
			scriptedit.Paste();
		}
		
		// This steals the focus (use with care!)
		public void GrabFocus()
		{
			scriptedit.GrabFocus();
		}
		
		#endregion
		
		#region ================== Events
		
		// Layout needs to be re-organized
		protected override void OnLayout(LayoutEventArgs e)
		{
			base.OnLayout(e);

			// With or without functions bar?
			if(functionbar.Visible)
			{
				scriptpanel.Top = functionbar.Bottom + 6;
				scriptpanel.Height = this.ClientSize.Height - scriptpanel.Top;
			}
			else
			{
				scriptpanel.Top = 0;
				scriptpanel.Height = this.ClientSize.Height;
			}
		}

		// Key pressed down
		private void scriptedit_KeyDown(object sender, KeyEventArgs e)
		{
			// CTRL+Space to autocomplete
			if((e.KeyCode == Keys.Space) && (e.Modifiers == Keys.Control))
			{
				// Hide call tip if any
				scriptedit.CallTipCancel();
				
				// Show autocomplete
				int currentpos = scriptedit.CurrentPos;
				int wordstartpos = scriptedit.WordStartPosition(currentpos, true);
				scriptedit.AutoCShow(currentpos - wordstartpos, autocompletestring);
				
				e.Handled = true;
			}
		}
		
		// Key released
		private void scriptedit_KeyUp(object sender, KeyEventArgs e)
		{
			bool showcalltip = false;
			int highlightstart = 0;
			int highlightend = 0;
			
			UpdatePositionInfo();
			
			// Call tip shown
			if(scriptedit.IsCallTipActive)
			{
				// Should we hide the call tip?
				if(curfunctionname.Length == 0)
				{
					// Hide the call tip
					scriptedit.CallTipCancel();
				}
				else
				{
					// Update the call tip
					showcalltip = true;
				}
			}
			// No call tip
			else
			{
				// Should we show a call tip?
				showcalltip = (curfunctionname.Length > 0) && !scriptedit.IsAutoCActive;
			}
			
			// Show or update call tip
			if(showcalltip)
			{
				string functiondef = scriptconfig.GetFunctionDefinition(curfunctionname);
				if(functiondef != null)
				{
					// Determine the range to highlight
					int argsopenpos = functiondef.IndexOf(scriptconfig.FunctionOpen);
					int argsclosepos = functiondef.LastIndexOf(scriptconfig.FunctionClose);
					if((argsopenpos > -1) && (argsclosepos > -1))
					{
						string argsstr = functiondef.Substring(argsopenpos + 1, argsclosepos - argsopenpos - 1);
						string[] args = argsstr.Split(scriptconfig.ArgumentDelimiter[0]);
						if((curargumentindex >= 0) && (curargumentindex < args.Length))
						{
							int argoffset = 0;
							for(int i = 0; i < curargumentindex; i++) argoffset += args[i].Length + 1;
							highlightstart = argsopenpos + argoffset + 1;
							highlightend = highlightstart + args[curargumentindex].Length;
						}
					}
					
					// Show tip
					scriptedit.CallTipShow(curfunctionstartpos, functiondef);
					scriptedit.CallTipSetHlt(highlightstart, highlightend);
				}
			}
		}
		
		#endregion
	}
}