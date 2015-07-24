// This is the main DLL file.

#include "stdafx.h"

#include "libsshnet.h"

namespace npcook {
	namespace libsshnet {
		Session::Session()
		{
			self = new ssh::Session();
		}

		Session::~Session()
		{
			this->!Session();
		}

		Session::!Session()
		{
			if (self != nullptr)
			{
				self->disconnect();
				delete self;
			}
		}

		void Session::SetOption(SshOption opt, String^ value)
		{
			auto unmanagedValue = msclr::interop::marshal_as<std::string>(value);
			try
			{
				self->setOption((ssh_options_e)opt, unmanagedValue.c_str());
			}
			catch (ssh::SshException ex)
			{
				throw gcnew SshException(&ex);
			}
		}

		void Session::SetOption(SshOption opt, int value)
		{
			self->setOption((ssh_options_e) opt, (long) value);
		}

		void Session::Connect()
		{
			try
			{
				self->connect();
			}
			catch (ssh::SshException ex)
			{
				throw gcnew SshException(&ex);
			}
		}

		void Session::PasswordAuth(String^ password)
		{
			try
			{
				auto unmanagedValue = msclr::interop::marshal_as<std::string>(password);
				self->userauthPassword(unmanagedValue.c_str());
			}
			catch (ssh::SshException ex)
			{
				throw gcnew SshException(&ex);
			}
		}

		ssh::Session& Session::getSession()
		{
			return *self;
		}

		Channel::Channel(Session^ session)
		{
			self = new ssh::Channel(session->getSession());
		}

		Channel::Channel(ssh::Channel* self)
			: self(self)
		{ }

		Channel::~Channel()
		{
			this->!Channel();
		}

		Channel::!Channel()
		{
			if (self != nullptr)
			{
				self->close();
				delete self;
				self = nullptr;
			}
		}

		bool Channel::EndOfFile::get()
		{
			return self->isEof();
		}

		bool Channel::IsOpen::get()
		{
			return self->isOpen();
		}

		bool Channel::IsClosed::get()
		{
			return self->isClosed();
		}

		int Channel::Read(array<byte>^ data, TimeSpan timeout)
		{
			return Read(data, 0, data->Length, timeout);
		}

		int Channel::Read(array<byte>^ data, int index, int length, TimeSpan timeout)
		{
			int ret = SSH_ERROR;
			byte* unmanagedBuffer = new byte[length];
			try
			{
				ret = self->read(unmanagedBuffer, length, (int) timeout.TotalMilliseconds);
				System::Runtime::InteropServices::Marshal::Copy(IntPtr(unmanagedBuffer), data, index, length);
			}
			catch (ssh::SshException ex)
			{
				throw gcnew SshException(&ex);
			}
			finally
			{
				delete[] unmanagedBuffer;
			}
			return ret;
		}

		int Channel::Write(array<byte>^ data, bool isStdErr)
		{
			return Write(data, 0, data->Length, isStdErr);
		}

		int Channel::Write(array<byte>^ data, int index, int length, bool isStdErr)
		{
			byte* unmanagedBuffer = new byte[length];
			System::Runtime::InteropServices::Marshal::Copy(data, index, IntPtr(unmanagedBuffer), length);
			try
			{
				return self->write(unmanagedBuffer, length, isStdErr);
			}
			catch (ssh::SshException ex)
			{
				throw gcnew SshException(&ex);
			}
			finally
			{
				delete[] unmanagedBuffer;
			}
		}

		Channel^ Channel::AcceptX11(TimeSpan timeout)
		{
			return gcnew Channel(self->acceptX11((int) timeout.TotalMilliseconds));
		}

		void Channel::RequestPty(String^ term, int cols, int rows)
		{
			auto unmanagedValue = msclr::interop::marshal_as<std::string>(term);
			self->requestPty(unmanagedValue.c_str(), cols, rows);
		}

		void Channel::RequestShell()
		{
			try
			{
				return self->requestShell();
			}
			catch (ssh::SshException ex)
			{
				throw gcnew SshException(&ex);
			}
		}

		void Channel::ChangePtySize(int cols, int rows)
		{
			self->changePtySize(cols, rows);
		}

		void Channel::Close()
		{
			self->close();
		}

		
		SshException::SshException(ssh::SshException* inner)
			: Exception(msclr::interop::marshal_as<String^>(inner->getError()))
		{
			code = inner->getCode();
		}

		int SshException::Code::get()
		{
			return code;
		}
	}
}