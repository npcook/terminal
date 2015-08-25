// libsshnet.h

#pragma once

#pragma managed(push, off)

#pragma warning(push)
#pragma warning(disable: 4267)

#include "libssh/libsshpp.hpp"

#pragma warning(pop)
#pragma managed(pop)

using namespace System;

namespace npcook {
	namespace libsshnet {
		public ref class SshException : public Exception
		{
		internal:
			SshException(ssh::SshException* inner);

		public:
			initonly int code;
			property int Code
			{ int get(); }
		};

		public enum class SshOption
		{
			Host = SSH_OPTIONS_HOST,	// const char*
			Port = SSH_OPTIONS_PORT,	// unsigned int
			User = SSH_OPTIONS_USER,	// const char*
			CiphersCS = SSH_OPTIONS_CIPHERS_C_S,	// const char*
		};

		public ref class Session : IDisposable
		{
			ssh::Session* self;
			~Session();
			!Session();

		public:
			Session();

			void SetOption(SshOption opt, String^ value);
			void SetOption(SshOption opt, int value);

			void Connect();

			void PasswordAuth(String^ password);

		internal:
			ssh::Session& getSession();
		};

		public ref class Channel : IDisposable
		{
			ssh::Channel* self;
			Channel(ssh::Channel* self);

		public:
			Channel(Session^ session);
			~Channel();
			!Channel();

			Channel^ AcceptX11(TimeSpan timeout);
			void Close();

			property bool IsClosed
			{ bool get(); }

			property bool EndOfFile
			{ bool get(); }

			property bool IsOpen
			{ bool get(); }

			int Read(array<byte>^ data, TimeSpan timeout);
			int Read(array<byte>^ data, int index, int length, TimeSpan timeout);
			int Write(array<byte>^ data, bool isStdErr);
			int Write(array<byte>^ data, int index, int length, bool isStdErr);

			void ChangePtySize(int cols, int rows);
			void RequestPty(String^ term, int cols, int rows);
			void RequestShell();
		};
	}
}