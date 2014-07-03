﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WetHatLab.OneNote.TaggingKit.edit;

namespace WetHatLab.OneNote.TaggingKit.common.ui
{

    /// <summary>
    /// Event details for the <see cref="E:TagInputBox.TagInput"/> event
    /// </summary>
    public class TagInputEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Determine if tag input is complete
        /// </summary>
        /// <value>true if tag input is complete; false if tag input is still in progress</value>
        public bool TagInputComplete { get; private set; }

        /// <summary>
        /// Create a new instance of the event details
        /// </summary>
        /// <param name="routedEvent">routed event which fired</param>
        /// <param name="source">object which fired the event</param>
        /// <param name="enterPressed">true if tag input is complete; false otherwise</param>
        internal TagInputEventArgs(RoutedEvent routedEvent, object source, bool enterPressed)
            : base(routedEvent, source)
        {
            TagInputComplete = enterPressed;
        }
    }
    /// <summary>
    /// handlers for the <see cref="E:TagInputBox.TagInput"/> event
    /// </summary>
    /// <param name="sender">object emitting the event</param>
    /// <param name="e">event details</param>
    public delegate void TagInputEventHandler(object sender, TagInputEventArgs e);

    /// <summary>
    /// Capture input of one or more tags separated by comma ','
    /// </summary>
    public partial class TagInputBox : UserControl
    {
        /// <summary>
        /// Routed event fired for changes to the <see cref="Tags"/> property
        /// </summary>
        public static readonly RoutedEvent TagInputEvent = EventManager.RegisterRoutedEvent("TagInput", RoutingStrategy.Bubble, typeof(TagInputEventHandler), typeof(TagInputBox));

        /// <summary>
        /// Routed event fired for changes to the <see cref="Tags"/> property
        /// </summary>
        public event TagInputEventHandler TagInput
        {
            add { AddHandler(TagInputEvent, value); }
            remove { RemoveHandler(TagInputEvent, value); }
        }

        /// <summary>
        /// Dependency property for the context tags source
        /// </summary>
        public static readonly DependencyProperty ContextTagsSourceProperty = DependencyProperty.Register("ContextTagsSource", typeof(TagsAndPages), typeof(TagInputBox),new PropertyMetadata(OnContextTagSourceChanged));

        private static void OnContextTagSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TagInputBox ib = d as TagInputBox;
            if (ib != null)
            {
                ib.presetsMenu.Visibility = e.NewValue != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Get or set a collection which provides tags from a OneNote context.
        /// </summary>
        public TagsAndPages ContextTagsSource
        {
            get
            {
                return GetValue(ContextTagsSourceProperty) as TagsAndPages;
            }
            set
            {
                SetValue(ContextTagsSourceProperty, value);
            }
        }

        /// <summary>
        /// Create a new instance of a input box for tag names.
        /// </summary>
        public TagInputBox()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Check if the tag input is empty
        /// </summary>
        /// <value>true if no input is available; false otherwie</value>
        internal bool IsEmpty
        {
            get
            {
                return string.IsNullOrEmpty(tagInput.Text);
            }
        }

        internal IEnumerable<string> Tags
        {
            get
            {
                return from t in OneNotePageProxy.ParseTags(tagInput.Text) select CultureInfo.CurrentCulture.TextInfo.ToTitleCase(t);
            }
            set
            {
                tagInput.Text = string.Join(",", value);

                UpdateVisibility();
            }
        }

        internal bool FocusInput()
        {
            return tagInput.Focus();
        }

        internal void Clear()
        {
            tagInput.Text = String.Empty;
            RaiseEvent(new TagInputEventArgs(TagInputEvent, this, enterPressed: false));
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            if (string.IsNullOrEmpty(tagInput.Text))
            {
                tagInput.Background = Brushes.Transparent;
                clearTagInput.Visibility = System.Windows.Visibility.Collapsed;
            }
            else
            {
                tagInput.Background = Brushes.White;
                clearTagInput.Visibility = System.Windows.Visibility.Visible;
            }
            tagInput.Focus();
        }

        private void TagInput_KeyUp(object sender, KeyEventArgs e)
        {
            UpdateVisibility();
            RaiseEvent(new TagInputEventArgs(TagInputEvent, this, e.Key == System.Windows.Input.Key.Enter));
            e.Handled = true;
        }

        private void ClearInputButton_Click(object sender, RoutedEventArgs e)
        {
            Clear();
            e.Handled = true;
        }

        private void handlePopupPointerAction(object sender, RoutedEventArgs e)
        {
            Popup p = sender as Popup;
            if (p != null)
            {
                p.IsOpen = false;
            }
            e.Handled = true;
        }

        private Task<IEnumerable<TagPageSet>> GetContextTagsAsync(TagContext filter)
        {
            TagsAndPages tags = ContextTagsSource;
            return Task<IEnumerable<TagPageSet>>.Run(() => { return GetContextTagsAction(filter,tags); });
        }

        private IEnumerable<TagPageSet> GetContextTagsAction(TagContext filter, TagsAndPages contextTags)
        {
            HashSet<TagPageSet> tags = new HashSet<TagPageSet>();

            contextTags.FindPages(contextTags.CurrentWindow.CurrentSectionId, includeUnindexedPages: true);

            switch (filter)
            {
                case TagContext.CurrentNote:
                    TaggedPage currentPage = (from p in contextTags.Pages where p.Key.Equals(contextTags.CurrentWindow.CurrentPageId) select p.Value).FirstOrDefault();
                    if (currentPage != null)
                    {
                        tags.UnionWith(currentPage.Tags);
                    }
                    break;
                case TagContext.SelectedNotes:
                    foreach (var p in (from pg in contextTags.Pages where pg.Value.IsSelected select pg.Value))
                    {
                        tags.UnionWith(p.Tags);
                    }
                    break;
                case TagContext.CurrentSection:
                    foreach (var p in contextTags.Pages)
                    {
                        tags.UnionWith(p.Value.Tags);
                    }
                    break;
            }
            return tags;
        }

        private async void Filter_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ContextTagsSource == null)
                {
                    return;
                }

                MenuItem itm = sender as MenuItem;

                TagContext filter = (TagContext)Enum.Parse(typeof(TagContext), itm.Tag.ToString());
                IEnumerable<TagPageSet> tags = await GetContextTagsAsync(filter);

                IEnumerable<string> tagNames = from t in tags select t.TagName;

                string taglist = string.Join(",", tagNames);
                tagInput.Text = taglist;
                if (string.IsNullOrEmpty(taglist))
                {
                    filterPopup.IsOpen = true;
                }
                UpdateVisibility();
                RaiseEvent(new TagInputEventArgs(TagInputEvent, this, enterPressed: false));
            }
            catch (Exception ex)
            {
                TraceLogger.Log(TraceCategory.Error(), "Applying preset filter failed {0}", ex);
                TraceLogger.ShowGenericMessageBox(Properties.Resources.TagEditor_Filter_Error, ex);
            }
            finally
            {
                e.Handled = true;
            }
        }
    }
}
