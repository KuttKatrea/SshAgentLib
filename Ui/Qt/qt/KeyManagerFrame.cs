using QtCore;
using QtGui;
using dlech.SshAgentLib;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace dlech.SshAgentLib.Ui.QtAgent
{
  public partial class KeyManagerFrame : QWidget
  {
    private IAgent mAgent;

    public KeyManagerFrame ()
    {
      SetupUi (this);
      mTableWidget.HorizontalHeader
        .SetResizeMode(QHeaderView.ResizeMode.ResizeToContents);
      mTableWidget.SelectionModel.SelectionChanged +=
        mTableWidget_SelectionModel_SelectionChanged;

      mLockButton.Clicked += mLockButton_Clicked;
      mUnlockButton.Clicked += mUnlockButton_Clicked;
      mAddButton.Clicked += mAddButton_Clicked;
      mRemoveButton.Clicked += mRemoveButton_Clicked;
      mRemoveAllButton.Clicked += mRemoveAllButton_Clicked;
      mRefreshButton.Clicked += mRefreshButton_Clicked;

      mTableWidget.DragEnterEvent += mTableWidget_DragEnterEvent;
      mTableWidget.DragMoveEvent += mTableWidget_DragMoveEvent;
      mTableWidget.DropEvent += mTableWidget_DropEvent;

      mMessageLabel.DragEnterEvent += mTableWidget_DragEnterEvent;
      mMessageLabel.DragMoveEvent += mTableWidget_DragMoveEvent;
      mMessageLabel.DropEvent += mTableWidget_DropEvent;
    }

    [Q_SLOT]
    private void mTableWidget_DragEnterEvent(object aSender,
                                             QEventArgs<QDragEnterEvent> aEventArgs)
    {
      if (aEventArgs.Event.MimeData.HasUrls)
      {
        aEventArgs.Event.DropAction = DropAction.CopyAction;
        aEventArgs.Event.Accept();
        aEventArgs.Event.AcceptProposedAction();
        aEventArgs.Handled = true;
      }
    }

    [Q_SLOT]
    private void mTableWidget_DragMoveEvent(object aSender,
                                            QEventArgs<QDragMoveEvent> aEventArgs)
    {
      if (aEventArgs.Event.MimeData.HasUrls)
      {
        aEventArgs.Event.DropAction = DropAction.CopyAction;
        aEventArgs.Event.Accept();
        aEventArgs.Event.AcceptProposedAction();
        aEventArgs.Handled = true;
      }
    }

    [Q_SLOT]
    private void mTableWidget_DropEvent(object aSender,
                                        QEventArgs<QDropEvent> aEventArgs)
    {
      if (aEventArgs.Event.MimeData.HasUrls) {
        foreach (var url in aEventArgs.Event.MimeData.Urls) {
          if (url.IsLocalFile) {
            var localFile = url.ToLocalFile();
            if (File.Exists(localFile)) {
              try {
                mAgent.AddKeyFromFile(localFile, null, null);
              } catch (Exception ex) {
                Debug.Fail(ex.ToString());
              }
            }
          }
        }
        aEventArgs.Event.AcceptProposedAction();
        aEventArgs.Handled = true;
        if (mAgent is AgentClient) {
          ReloadData();
        }
      }
    }

    public void SetAgent (IAgent aAgent)
    {
      mAgent = aAgent;
      var agent = aAgent as Agent;
      if (agent == null) {
        mTableWidget.HideColumn (0);
        mTableWidget.HideColumn (1);
      } else {
        mTableWidget.ShowColumn (0);
        mTableWidget.ShowColumn (1);
      }
      ReloadData ();
    }

    [Q_SLOT]
    private void mTableWidget_SelectionModel_SelectionChanged (
      QItemSelection aSelected, QItemSelection aDeselected)
    {
      UpdateUIState ();
    }

    private void ReloadData ()
    {
      mTableWidget.RowCount = 0;
      foreach (var key in mAgent.GetAllKeys ()) {
        var newRowIndex = mTableWidget.RowCount;
        mTableWidget.Model.InsertRow (newRowIndex);
        // TODO - make checkboxes
        mTableWidget.SetItem (newRowIndex, 0, new QTableWidgetItem(
          key.HasConstraint (Agent.KeyConstraintType.SSH_AGENT_CONSTRAIN_CONFIRM).ToString ()));
        mTableWidget.SetItem (newRowIndex, 1, new QTableWidgetItem(
          key.HasConstraint (Agent.KeyConstraintType.SSH_AGENT_CONSTRAIN_LIFETIME).ToString ()));
        mTableWidget.SetItem (newRowIndex, 2, new QTableWidgetItem(
          key.Algorithm.GetIdentifierString ()));
        mTableWidget.SetItem (newRowIndex, 3, new QTableWidgetItem(
          key.Size.ToString ()));
        mTableWidget.SetItem (newRowIndex, 4, new QTableWidgetItem(
          key.GetMD5Fingerprint ().ToHexString ()));
        mTableWidget.SetItem (newRowIndex, 5, new QTableWidgetItem(
          key.Comment));
        // attach actual key object to arbitrary column for later retreval
        mTableWidget.Item (newRowIndex, 0).SetData ((int)Qt.ItemDataRole.UserRole , key);
      }
      UpdateUIState ();
    }

    private void UpdateUIState ()
    {
      var agent = mAgent as Agent;
      if (agent == null) {
        mLockButton.Enabled = true;
        mUnlockButton.Enabled = true;
        mRefreshButton.Visible = true;
        mMessageLabel.Text = Tr ("No keys loaded");
      } else {
        mLockButton.Enabled = agent.IsLocked;
        mUnlockButton.Enabled = !agent.IsLocked;
        mRefreshButton.Visible = false;
        mMessageLabel.Text = agent.IsLocked ? Tr ("Locked") : Tr ("No keys loaded");
      }
      mStackedWidget.CurrentIndex = mTableWidget.RowCount > 0 ? 1 : 0;
      mRemoveButton.Enabled = mTableWidget.SelectedIndexes.Count > 0;
      mRemoveAllButton.Enabled = mTableWidget.RowCount > 0;
    }

    [Q_SLOT]
    private void mLockButton_Clicked ()
    {
      using (var dialog = new PassphraseDialog ()) {
        dialog.Exec ();
        if (dialog.Result == (int)QDialog.DialogCode.Rejected) {
          return;
        }
        // TODO - add "are you sure" for empty passphrase
        var passphrase = dialog.GetPassphrase();
        try {
          mAgent.Lock (passphrase);
        } catch (AgentLockedException) {
          QMessageBox.Critical (this, Tr("Error"), Tr("Agent is already locked"));
          Debug.Fail ("Lock button should have been disabled");
        } catch (AgentFailureException) {
          QMessageBox.Critical (this, Tr("Agent Failure"),
                                Tr("Possible causes:") +
                                "<ul>" +
                                "<li>" + Tr("Agent is already locked") + "</li>" +
                                "<li>" + Tr("Agent does not support locking") + "</li>" +
                                "</ul>"
          );
        } catch (Exception ex) {
          Debug.Fail (ex.ToString ());
        }
      }
      if (mAgent is Agent) {
        UpdateUIState ();
      } else {
        ReloadData ();
      }
    }

    [Q_SLOT]
    private void mUnlockButton_Clicked ()
    {
      using (var dialog = new PassphraseDialog ()) {
        dialog.Exec ();
        if (dialog.Result == (int)QDialog.DialogCode.Rejected) {
          return;
        }
        var passphrase = dialog.GetPassphrase();
        try {
          mAgent.Unlock (passphrase);
        } catch (AgentLockedException) {
          QMessageBox.Critical (this, Tr("Error"), Tr("Agent is already locked"));
          Debug.Fail ("Unlock button should have been disabled");
        } catch (AgentFailureException) {
          QMessageBox.Critical (this, Tr("Agent Failure"),
                                Tr("Possible causes:") +
                                "<ul>" +
                                "<li>" + Tr("Passphrase was incorrect") + "</li>" +
                                "<li>" + Tr("Agent is already unlocked") + "</li>" +
                                "<li>" + Tr("Agent does not support locking") + "</li>" +
                                "</ul>"
          );
        } catch (Exception ex) {
          Debug.Fail (ex.ToString ());
        }
      }
      if (mAgent is Agent) {
        UpdateUIState ();
      } else {
        ReloadData ();
      }
    }

    [Q_SLOT]
    private void mAddButton_Clicked ()
    {
      // TODO - persist start directory (and possibly viewMode)
      using (var dialog = new KeyFileDialog()) {
        dialog.SetDirectory (Environment.GetFolderPath(
                             Environment.SpecialFolder.Personal));
        dialog.Exec ();
        if (dialog.Result  == (int)QDialog.DialogCode.Accepted) {
          var constraints = dialog.GetConstraints ();
          foreach (var file in dialog.SelectedFiles) {
            try {
              // TODO - add passphrase callback
              mAgent.AddKeyFromFile (file, null, constraints);
            } catch (AgentFailureException) {
              QMessageBox.Critical (this,
                                    Tr("Agent Failure"),
                                    Tr("Possible causes:") +
                                    "<ul>" + "</li>" +
                                    "<li>" + Tr("Agent is locked") + "</li>" +
                                    "<li>" + Tr("Agent does not support this key type") +
                                    "</ul>");
            } catch (KeyFormatterException) {
              QMessageBox.Critical (this,
                                    Tr("File format error"),
                                    Tr("This file not a recognized private key file") +
                                    "<br><br>" +
                                    file);
            } catch (Exception ex) {
              Debug.Fail (ex.ToString ());
            }
          }
        }
      }
      if (mAgent is Agent) {
        UpdateUIState ();
      } else {
        ReloadData ();
      }
    }

    [Q_SLOT]
    private void mRemoveButton_Clicked ()
    {
      foreach (var index in mTableWidget.SelectionModel.SelectedRows ()) {
        try {
          var key = mTableWidget.Item (index.Row, 0)
            .Data ((int)Qt.ItemDataRole.UserRole) as ISshKey;
          mAgent.RemoveKey (key);
        } catch (AgentFailureException) {
          QMessageBox.Critical (this, Tr ("Agent Failure"),
                                Tr ("Possible causes:") +
                                "<ul>" +
                                "<li>" + Tr ("Agent is locked") + "</li>" +
                                "<li>" + Tr ("Key may have already been removed") + "</li>" +
                                "</ul>"
          );
        } catch (Exception ex) {
          Debug.Fail (ex.ToString ());
        }
    }
      if (mAgent is Agent) {
        UpdateUIState ();
      } else {
        ReloadData ();
      }
    }

    [Q_SLOT]
    private void mRemoveAllButton_Clicked ()
    {
      try {
        mAgent.RemoveAllKeys ();
      } catch (AgentFailureException) {
        QMessageBox.Critical (this, Tr ("Agent Failure"),
                              Tr ("Possible causes:") +
                              "<ul>" +
                              "<li>" + Tr ("Agent is locked") + "</li>" +
                              "<li>" + Tr ("Agent does not support removing all keys") + "</li>" +
                              "</ul>"
        );
      } catch (Exception ex) {
        Debug.Fail (ex.ToString ());
      }
      if (mAgent is Agent) {
        UpdateUIState ();
      } else {
        ReloadData ();
      }
    }

    [Q_SLOT]
    private void mRefreshButton_Clicked ()
    {
      Debug.Assert (mAgent is AgentClient);
      ReloadData ();
    }

  }
}
