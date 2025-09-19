import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { User, Mail, Lock, UserCheck } from 'lucide-react';
import Input from '../common/Input';
import Button from '../common/Button';
import toast from 'react-hot-toast';

const RegisterForm: React.FC = () => {
  const [formData, setFormData] = useState({
    displayName: '',
    username: '',
    email: '',
    password: '',
    confirmPassword: '',
  });

  // Password validation rules
  const passwordRules = [
    {
      label: 'At least 8 characters',
      validate: (pw: string) => pw.length >= 8,
    },
    {
      label: 'One uppercase letter',
      validate: (pw: string) => /[A-Z]/.test(pw),
    },
    {
      label: 'One lowercase letter',
      validate: (pw: string) => /[a-z]/.test(pw),
    },
    {
      label: 'One number',
      validate: (pw: string) => /[0-9]/.test(pw),
    },
  ];
  const passwordValidations = passwordRules.map(rule => rule.validate(formData.password));
  const [isLoading, setIsLoading] = useState(false);
  const { register } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    

    if (formData.password !== formData.confirmPassword) {
      toast.error('Passwords do not match');
      return;
    }
    // Password validation
    if (!passwordValidations.every(Boolean)) {
      toast.error('Password does not meet requirements');
      return;
    }

    setIsLoading(true);

    try {
      await register({
        displayName: formData.displayName,
        username: formData.username,
        email: formData.email,
        password: formData.password,
      });
      
      toast.success('Account created successfully! Please log in.');
      navigate('/login');
    } catch (error: any) {
      toast.error(error.message || 'Registration failed');
    } finally {
      setIsLoading(false);
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFormData(prev => ({
      ...prev,
      [e.target.name]: e.target.value
    }));
  };

  const isFormValid = Object.values(formData).every(value => value.trim() !== '');

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-blue-50 via-white to-purple-50 dark:from-gray-900 dark:via-gray-800 dark:to-purple-900 py-12 px-4 sm:px-6 lg:px-8 relative overflow-hidden">
      {/* Background decoration */}
      <div className="absolute inset-0 -z-10">
        <div className="absolute top-1/4 right-1/4 w-96 h-96 bg-purple-400/20 dark:bg-purple-500/10 rounded-full blur-3xl"></div>
        <div className="absolute bottom-1/4 left-1/4 w-80 h-80 bg-blue-400/20 dark:bg-blue-500/10 rounded-full blur-3xl"></div>
      </div>
      
      <div className="max-w-md w-full space-y-8 relative">
        {/* Logo/Header Section */}
        <div className="text-center">
          <div className="mx-auto w-16 h-16 bg-gradient-to-br from-blue-600 to-purple-600 rounded-full flex items-center justify-center shadow-lg mb-6">
            <div className="w-8 h-8 bg-white rounded-full flex items-center justify-center">
              <div className="w-0 h-0 border-l-[6px] border-l-transparent border-r-[6px] border-r-transparent border-b-[10px] border-b-blue-600 ml-1"></div>
            </div>
          </div>
          <h2 className="text-4xl font-bold mb-2">
            <span className="bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
              Join StreamSync
            </span>
          </h2>
          <p className="text-gray-600 dark:text-gray-300 mb-2">
            Create your account and start watching together
          </p>
          <p className="text-sm text-gray-500 dark:text-gray-400">
            Already have an account?{' '}
            <Link
              to="/login"
              className="font-medium bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent hover:from-blue-700 hover:to-purple-700 transition-all duration-200"
            >
              Sign in here
            </Link>
          </p>
        </div>
        
        {/* Register Card */}
        <div className="bg-white/80 dark:bg-gray-900/80 backdrop-blur-xl rounded-2xl shadow-2xl border border-gray-200/50 dark:border-gray-700/50 p-8 relative overflow-hidden">
          {/* Card decoration */}
          <div className="absolute top-0 right-0 w-32 h-32 bg-gradient-to-br from-purple-500/10 to-blue-500/10 rounded-full blur-2xl"></div>
          <div className="absolute bottom-0 left-0 w-24 h-24 bg-gradient-to-tr from-blue-500/10 to-purple-500/10 rounded-full blur-2xl"></div>
          
          <form className="space-y-5 relative" onSubmit={handleSubmit}>
            <Input
              label="Display Name"
              type="text"
              name="displayName"
              value={formData.displayName}
              onChange={handleChange}
              icon={UserCheck}
              placeholder="How others will see you"
              required
              className="bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm border-gray-200/50 dark:border-gray-700/50 focus:border-blue-500 focus:ring-blue-500/20"
            />
            
            <Input
              label="Username"
              type="text"
              name="username"
              value={formData.username}
              onChange={handleChange}
              icon={User}
              placeholder="Choose a username"
              required
              className="bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm border-gray-200/50 dark:border-gray-700/50 focus:border-blue-500 focus:ring-blue-500/20"
            />
            
            <Input
              label="Email"
              type="email"
              name="email"
              value={formData.email}
              onChange={handleChange}
              icon={Mail}
              placeholder="Enter your email"
              required
              className="bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm border-gray-200/50 dark:border-gray-700/50 focus:border-blue-500 focus:ring-blue-500/20"
            />
            
            <Input
              label="Password"
              type="password"
              name="password"
              value={formData.password}
              onChange={handleChange}
              icon={Lock}
              placeholder="Choose a password"
              required
              className="bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm border-gray-200/50 dark:border-gray-700/50 focus:border-blue-500 focus:ring-blue-500/20"
            />
            {/* Password validation UI: only show when password is not empty */}
            {formData.password && (
              <div className="mt-2 mb-4 p-4 rounded-xl bg-black/10 dark:bg-white/5 border border-gray-200/30 dark:border-gray-700/30">
                {passwordRules.map((rule, idx) => (
                  <div key={rule.label} className="flex items-center gap-2 mb-1 last:mb-0">
                    <span className={`w-2 h-2 rounded-full mr-2 ${passwordValidations[idx] ? 'bg-green-400' : 'bg-gray-400'}`}></span>
                    <span className={`text-sm ${passwordValidations[idx] ? 'text-green-400' : 'text-gray-400'}`}>{rule.label}</span>
                  </div>
                ))}
              </div>
            )}
            
            <Input
              label="Confirm Password"
              type="password"
              name="confirmPassword"
              value={formData.confirmPassword}
              onChange={handleChange}
              icon={Lock}
              placeholder="Confirm your password"
              required
              className="bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm border-gray-200/50 dark:border-gray-700/50 focus:border-blue-500 focus:ring-blue-500/20"
            />

            <Button
              type="submit"
              variant="gradient"
              className="w-full mt-6"
              isLoading={isLoading}
              disabled={!isFormValid}
            >
              {isLoading ? 'Creating Account...' : 'Create Account'}
            </Button>
          </form>
        </div>
      </div>
    </div>
  );
};

export default RegisterForm;
