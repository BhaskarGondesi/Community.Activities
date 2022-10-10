﻿using System;
using System.Activities;
using System.Activities.Expressions;
using System.Activities.Validation;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using UiPath.Cryptography.Activities.Models;
using UiPath.Cryptography.Activities.Properties;
using UiPath.Cryptography.Enums;
using UiPath.Platform.ResourceHandling;

namespace UiPath.Cryptography.Activities
{
    [LocalizedDisplayName(nameof(Resources.Activity_EncryptFile_Name))]
    [LocalizedDescription(nameof(Resources.Activity_EncryptFile_Description))]
    public partial class EncryptFile : CodeActivity
    {

        public EncryptFile()
        {
            Algorithm = SymmetricAlgorithms.AESGCM;
            KeyEncoding = new InArgument<Encoding>(ExpressionServices.Convert((env) => Encoding.UTF8));
        }

        [RequiredArgument]
        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.Activity_EncryptFile_Property_Algorithm_Name))]
        [LocalizedDescription(nameof(Resources.Activity_EncryptFile_Property_Algorithm_Description))]
        public SymmetricAlgorithms Algorithm { get; set; }

        [RequiredArgument]
        [OverloadGroup(nameof(InputFilePath))]
        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.Activity_EncryptFile_Property_InputFilePath_Name))]
        [LocalizedDescription(nameof(Resources.Activity_EncryptFile_Property_InputFilePath_Description))]
        public InArgument<string> InputFilePath { get; set; }

        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.Activity_EncryptFile_Property_Key_Name))]
        [LocalizedDescription(nameof(Resources.Activity_EncryptFile_Property_Key_Description))]
        public InArgument<string> Key { get; set; }

        [Browsable(false)]
        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.Activity_EncryptFile_Property_KeyInputModeSwitch_Name))]
        [LocalizedDescription(nameof(Resources.Activity_EncryptFile_Property_KeyInputModeSwitch_Description))]
        public KeyInputMode KeyInputModeSwitch { get; set; }

        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.Activity_EncryptFile_Property_KeySecureString_Name))]
        [LocalizedDescription(nameof(Resources.Activity_EncryptFile_Property_KeySecureString_Description))]
        public InArgument<SecureString> KeySecureString { get; set; }

        [RequiredArgument]
        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.Activity_EncryptFile_Property_KeyEncoding_Name))]
        [LocalizedDescription(nameof(Resources.Activity_EncryptFile_Property_KeyEncoding_Description))]
        public InArgument<Encoding> KeyEncoding { get; set; }

        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.Activity_EncryptFile_Property_OutputFilePath_Name))]
        [LocalizedDescription(nameof(Resources.Activity_EncryptFile_Property_OutputFilePath_Description))]
        public InArgument<string> OutputFilePath { get; set; }

        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.Activity_EncryptFile_Property_OutputFileName_Name))]
        [LocalizedDescription(nameof(Resources.Activity_EncryptFile_Property_OutputFileName_Description))]
        public InArgument<string> OutputFileName { get; set; }

        [RequiredArgument]
        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.Activity_EncryptFile_Property_Overwrite_Name))]
        [LocalizedDescription(nameof(Resources.Activity_EncryptFile_Property_Overwrite_Description))]
        public bool Overwrite { get; set; }

        [DefaultValue(null)]
        [LocalizedCategory(nameof(Resources.Common))]
        [LocalizedDisplayName(nameof(Resources.Activity_EncryptFile_Property_ContinueOnError_Name))]
        [LocalizedDescription(nameof(Resources.Activity_EncryptFile_Property_ContinueOnError_Description))]
        public InArgument<bool> ContinueOnError { get; set; }

        [Browsable(false)]
        [RequiredArgument]
        [OverloadGroup(nameof(InputFile))]
        [DefaultValue(null)]
        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.Activity_EncryptFile_Property_InputFile_Name))]
        [LocalizedDescription(nameof(Resources.Activity_EncryptFile_Property_InputFile_Description))]
        public InArgument<IResource> InputFile { get; set; }

        [Browsable(false)]
        [DefaultValue(null)]
        [LocalizedCategory(nameof(Resources.Output))]
        [LocalizedDisplayName(nameof(Resources.Activity_EncryptFile_Property_EncryptedFile_Name))]
        [LocalizedDescription(nameof(Resources.Activity_EncryptFile_Property_EncryptedFile_Description))]
        public OutArgument<ILocalResource> EncryptedFile { get; set; }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            if (!CryptographyHelper.IsFipsCompliant(Algorithm))
            {
                var error = new ValidationError(Resources.FipsComplianceWarning, true, nameof(Algorithm));

                metadata.AddValidationError(error);
            }

            if (Key == null && KeyInputModeSwitch == KeyInputMode.Key)
            {
                var error = new ValidationError(Resources.KeyNullError, false, nameof(Key));
                metadata.AddValidationError(error);
            }
            if (KeySecureString == null && KeyInputModeSwitch == KeyInputMode.SecureKey)
            {
                var error = new ValidationError(Resources.KeySecureStringNullError, false, nameof(KeySecureString));
                metadata.AddValidationError(error);
            }
        }

        protected override void Execute(CodeActivityContext context)
        {
            try
            {
                var inputFilePath = InputFilePath.Get(context);
                var inputFile = InputFile.Get(context);
                var outputFilePath = OutputFilePath.Get(context);
                var outputFileName = OutputFileName.Get(context);
                var key = Key.Get(context);
                var keySecureString = KeySecureString.Get(context);
                var keyEncoding = KeyEncoding.Get(context);

                if (string.IsNullOrWhiteSpace(key) && KeyInputModeSwitch == KeyInputMode.Key)
                {
                    throw new ArgumentNullException(Resources.Activity_KeyedHashText_Property_Key_Name);
                }
                if ((keySecureString == null || keySecureString?.Length == 0) && KeyInputModeSwitch == KeyInputMode.SecureKey)
                {
                    throw new ArgumentNullException(Resources.Activity_KeyedHashText_Property_KeySecureString_Name);
                }

                if (keyEncoding == null) throw new ArgumentNullException(Resources.Encoding);

                if (!File.Exists(inputFilePath) && inputFile == null)
                    throw new ArgumentException(Resources.FileDoesNotExistsException,
                        Resources.InputFilePathDisplayName);

                // Because we use File.WriteAllText below, we don't need to delete the file now.
                if (File.Exists(outputFilePath) && !Overwrite)
                    throw new ArgumentException(Resources.FileAlreadyExistsException,
                        Resources.OutputFilePathDisplayName);

                if (inputFile != null && inputFile.IsFolder)
                    throw new ArgumentException(Resources.Exception_UseOnlyFilesNotFolders);

                string fileName = string.Empty;

                if (inputFile != null && !inputFile.IsFolder)
                {
                    // Get local file
                    var localFile = inputFile.ToLocalResource();
                    //Resolve Sync
                    Task.Run(async () => await localFile.ResolveAsync()).GetAwaiter().GetResult();

                    //take the path from the resource
                    inputFilePath = localFile.LocalPath;
                    fileName = localFile.FullName;
                }

                if (outputFileName != null)
                {
                    fileName = outputFileName;
                }

                var encrypted = CryptographyHelper.EncryptData(Algorithm, File.ReadAllBytes(inputFilePath),
                    CryptographyHelper.KeyEncoding(keyEncoding, key, keySecureString));

                if (string.IsNullOrEmpty(outputFilePath))
                {
                    var item = new CryptographyLocalItem(encrypted, fileName);

                    EncryptedFile.Set(context, item);
                    
                    outputFilePath = item.LocalPath;

                }

                // This overwrites the file if it already exists.
                File.WriteAllBytes(outputFilePath, encrypted);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());

                if (!ContinueOnError.Get(context)) throw;
            }
        }
    }
}