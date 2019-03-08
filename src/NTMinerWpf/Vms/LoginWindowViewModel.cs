﻿using NTMiner.Views;
using System;
using System.Windows;
using System.Windows.Input;

namespace NTMiner.Vms {
    public class LoginWindowViewModel : ViewModelBase {
        private string _hostAndPort;
        private string _loginName;
        private string _message;
        private Visibility _messageVisible = Visibility.Collapsed;
        private string _password;
        private Visibility _isPasswordAgainVisible = Visibility.Collapsed;
        private string _passwordAgain;

        public ICommand Login { get; private set; }
        public ICommand ActiveAdmin { get; private set; }

        public LoginWindowViewModel() {
            this._hostAndPort = $"{Server.MinerServerHost}:{WebApiConst.MinerServerPort}";
            this._loginName = "admin";
            this.Login = new DelegateCommand(() => {
                string passwordSha1 = HashUtil.Sha1(Password);
                Server.ControlCenterService.LoginAsync(LoginName, passwordSha1, (response, exception) => {
                    UIThread.Execute(() => {
                        if (response == null) {
                            Message = "服务器忙";
                            MessageVisible = Visibility.Visible;
                            TimeSpan.FromSeconds(2).Delay().ContinueWith(t => {
                                UIThread.Execute(() => {
                                    MessageVisible = Visibility.Collapsed;
                                });
                            });
                            return;
                        }
                        if (response.IsSuccess()) {
                            SingleUser.LoginName = LoginName;
                            SingleUser.SetPasswordSha1(passwordSha1);
                            Window window = TopWindow.GetTopWindow();
                            if (window != null && window.GetType() == typeof(LoginWindow)) {
                                window.DialogResult = true;
                                window.Close();
                            }
                        }
                        else if (this.LoginName == "admin" && response != null && response.StateCode == 404) {
                            IsPasswordAgainVisible = Visibility.Visible;
                            Message = response.Description;
                            MessageVisible = Visibility.Visible;
                            TimeSpan.FromSeconds(2).Delay().ContinueWith(t => {
                                UIThread.Execute(() => {
                                    MessageVisible = Visibility.Collapsed;
                                });
                            });
                        }
                        else {
                            IsPasswordAgainVisible = Visibility.Collapsed;
                            Message = response.Description;
                            MessageVisible = Visibility.Visible;
                            TimeSpan.FromSeconds(2).Delay().ContinueWith(t => {
                                UIThread.Execute(() => {
                                    MessageVisible = Visibility.Collapsed;
                                });
                            });
                        }
                    });
                });
            });
            this.ActiveAdmin = new DelegateCommand(() => {
                string message = string.Empty;
                if (string.IsNullOrEmpty(this.Password)) {
                    message = "密码不能为空";
                }
                else if (this.Password != this.PasswordAgain) {
                    message = "两次输入的密码不一致";
                }
                if (!string.IsNullOrEmpty(message)) {
                    this.Message = message;
                    MessageVisible = Visibility.Visible;
                    TimeSpan.FromSeconds(2).Delay().ContinueWith(t => {
                        UIThread.Execute(() => {
                            MessageVisible = Visibility.Collapsed;
                        });
                    });
                    return;
                }
                string passwordSha1 = HashUtil.Sha1(Password);
                Server.ControlCenterService.ActiveControlCenterAdminAsync(passwordSha1, (response, e) => {
                    if (response.IsSuccess()) {
                        IsPasswordAgainVisible = Visibility.Collapsed;
                        this.Login.Execute(null);
                    }
                    else {
                        Message = response != null ? response.Description : "激活失败";
                        MessageVisible = Visibility.Visible;
                        TimeSpan.FromSeconds(2).Delay().ContinueWith(t => {
                            UIThread.Execute(() => {
                                MessageVisible = Visibility.Collapsed;
                            });
                        });
                    }
                });
            });
        }

        public LangViewModels LangVms {
            get { return LangViewModels.Current; }
        }

        public Visibility IsPasswordAgainVisible {
            get => _isPasswordAgainVisible;
            set {
                _isPasswordAgainVisible = value;
                OnPropertyChanged(nameof(IsPasswordAgainVisible));
            }
        }

        public string HostAndPort {
            get => _hostAndPort;
            set {
                if (_hostAndPort != value) {
                    _hostAndPort = value;
                    OnPropertyChanged(nameof(HostAndPort));
                }
            }
        }

        public string LoginName {
            get => _loginName;
            set {
                if (_loginName != value) {
                    _loginName = value;
                    OnPropertyChanged(nameof(LoginName));
                }
            }
        }

        public string Password {
            get => _password;
            set {
                _password = value;
                OnPropertyChanged(nameof(Password));
            }
        }

        public string PasswordAgain {
            get => _passwordAgain;
            set {
                _passwordAgain = value;
                OnPropertyChanged(nameof(PasswordAgain));
            }
        }

        public string Message {
            get => _message;
            set {
                if (_message != value) {
                    _message = value;
                    OnPropertyChanged(nameof(Message));
                }
            }
        }

        public Visibility MessageVisible {
            get => _messageVisible;
            set {
                if (_messageVisible != value) {
                    _messageVisible = value;
                    OnPropertyChanged(nameof(MessageVisible));
                }
            }
        }
    }
}
