# Frontend Validation with Zod + React Hook Form

Complete guide to implementing client-side validation with Zod and React Hook Form in DrimAgents.

## Basic Zod Schema

Mirror the backend FluentValidation rules in Zod for client-side validation.

```typescript
// lib/validations/post.ts
import { z } from 'zod';

export const createPostSchema = z.object({
  title: z
    .string()
    .min(1, 'Title is required')
    .max(200, 'Title must be 200 characters or less'),

  slug: z
    .string()
    .min(1, 'Slug is required')
    .regex(/^[a-z0-9-]+$/, 'Slug must be lowercase letters, numbers, and hyphens only'),

  content: z
    .string()
    .min(1, 'Content is required'),

  authorEmail: z
    .string()
    .email('Invalid email format'),

  website: z
    .string()
    .url('Invalid URL format')
    .optional()
    .or(z.literal('')),
});

export type CreatePostInput = z.infer<typeof createPostSchema>;
```

## Integration with React Hook Form

```typescript
// components/CreatePostForm.tsx
'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { createPostSchema, type CreatePostInput } from '@/lib/validations/post';

export function CreatePostForm() {
  const form = useForm<CreatePostInput>({
    resolver: zodResolver(createPostSchema),
    defaultValues: {
      title: '',
      slug: '',
      content: '',
      authorEmail: '',
      website: ''
    }
  });

  async function onSubmit(data: CreatePostInput) {
    try {
      const response = await fetch('/api/posts', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
      });

      if (!response.ok) {
        if (response.status === 400) {
          const problem = await response.json();
          handleProblemDetails(problem);
          return;
        }

        throw new Error('Failed to create post');
      }

      const result = await response.json();
    } catch (error) {
      console.error('Error creating post:', error);
    }
  }

  function handleProblemDetails(problem: any) {
    if (problem.errors) {
      Object.entries(problem.errors).forEach(([field, messages]) => {
        form.setError(field as keyof CreatePostInput, {
          type: 'server',
          message: (messages as string[])[0]
        });
      });
    }
  }

  return (
    <form onSubmit={form.handleSubmit(onSubmit)}>
      <div>
        <label htmlFor="title">Title</label>
        <input
          id="title"
          {...form.register('title')}
          aria-invalid={form.formState.errors.title ? 'true' : 'false'}
        />
        {form.formState.errors.title && (
          <p className="error">{form.formState.errors.title.message}</p>
        )}
      </div>

      <div>
        <label htmlFor="slug">Slug</label>
        <input
          id="slug"
          {...form.register('slug')}
          aria-invalid={form.formState.errors.slug ? 'true' : 'false'}
        />
        {form.formState.errors.slug && (
          <p className="error">{form.formState.errors.slug.message}</p>
        )}
      </div>

      <button type="submit" disabled={form.formState.isSubmitting}>
        {form.formState.isSubmitting ? 'Creating...' : 'Create Post'}
      </button>
    </form>
  );
}
```

## File Upload Validation

```typescript
// lib/validations/avatar.ts
import { z } from 'zod';

const MAX_FILE_SIZE = 5 * 1024 * 1024; // 5 MB
const ALLOWED_MIME_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

export const uploadAvatarSchema = z.object({
  avatar: z
    .instanceof(File)
    .refine((file) => file.size > 0, 'Avatar is required')
    .refine((file) => file.size <= MAX_FILE_SIZE, 'File size must not exceed 5 MB')
    .refine(
      (file) => ALLOWED_MIME_TYPES.includes(file.type),
      'File must be JPEG, PNG, or WebP'
    )
});

export type UploadAvatarInput = z.infer<typeof uploadAvatarSchema>;
```

## Password Validation

```typescript
// lib/validations/auth.ts
import { z } from 'zod';

export const registerSchema = z.object({
  email: z
    .string()
    .email('Invalid email format'),

  password: z
    .string()
    .min(8, 'Password must be at least 8 characters')
    .regex(/[A-Z]/, 'Password must contain at least one uppercase letter')
    .regex(/[a-z]/, 'Password must contain at least one lowercase letter')
    .regex(/[0-9]/, 'Password must contain at least one number'),

  passwordConfirmation: z.string()
}).refine((data) => data.password === data.passwordConfirmation, {
  message: 'Passwords do not match',
  path: ['passwordConfirmation']
});

export type RegisterInput = z.infer<typeof registerSchema>;
```

## Error Code Mapping for i18n

```typescript
// lib/i18n/error-messages.ts
export const ERROR_MESSAGES: Record<string, string> = {
  'blog:post:title:required': 'Title is required',
  'blog:post:title:too_long': 'Title must be 200 characters or less',
  'blog:post:slug:required': 'Slug is required',
  'blog:post:slug:invalid_format': 'Slug must be lowercase letters, numbers, and hyphens only',
  'blog:post:slug:already_exists': 'A post with this slug already exists',

  'users:email:required': 'Email is required',
  'users:email:invalid_format': 'Invalid email format',
  'users:password:required': 'Password is required',
  'users:password:too_short': 'Password must be at least 8 characters',
  'users:password:missing_uppercase': 'Password must contain at least one uppercase letter',
  'users:password_confirmation:mismatch': 'Passwords do not match',
  'users:avatar:file_too_large': 'File size must not exceed 5 MB',
  'users:avatar:invalid_mime_type': 'File must be JPEG, PNG, or WebP'
};

export function getErrorMessage(errorCode: string, fallback: string): string {
  return ERROR_MESSAGES[errorCode] || fallback;
}
```

## Frontend Tests

```typescript
// __tests__/CreatePostForm.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CreatePostForm } from '@/components/CreatePostForm';

describe('CreatePostForm', () => {
  it('shows client-side validation errors', async () => {
    const user = userEvent.setup();
    render(<CreatePostForm />);

    const submitButton = screen.getByRole('button', { name: /create/i });
    await user.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('Title is required')).toBeInTheDocument();
      expect(screen.getByText('Slug is required')).toBeInTheDocument();
    });
  });

  it('shows server-side validation errors', async () => {
    const user = userEvent.setup();

    global.fetch = jest.fn().mockResolvedValue({
      ok: false,
      status: 400,
      json: async () => ({
        type: 'https://tools.ietf.org/html/rfc7807',
        title: 'One or more validation errors occurred',
        status: 400,
        errors: {
          slug: ['A post with this slug already exists']
        },
        errorCodes: {
          slug: ['blog:post:slug:already_exists']
        }
      })
    });

    render(<CreatePostForm />);

    await user.type(screen.getByLabelText(/title/i), 'Test Post');
    await user.type(screen.getByLabelText(/slug/i), 'existing-slug');
    await user.type(screen.getByLabelText(/content/i), 'Test content');

    await user.click(screen.getByRole('button', { name: /create/i }));

    await waitFor(() => {
      expect(screen.getByText('A post with this slug already exists')).toBeInTheDocument();
    });
  });
});
```
