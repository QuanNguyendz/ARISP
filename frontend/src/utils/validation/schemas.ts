import { z } from 'zod';

export const loginSchema = z.object({
  email: z.string().email('Email không hợp lệ'),
  password: z.string().min(6, 'Mật khẩu phải có ít nhất 6 ký tự'),
});

export const jobPostingSchema = z.object({
  title: z.string().min(5, 'Tiêu đề phải có ít nhất 5 ký tự').max(200),
  description: z.string().min(50, 'Mô tả phải có ít nhất 50 ký tự'),
  requirements: z.array(z.string()).min(1, 'Phải có ít nhất 1 yêu cầu'),
  skills: z.array(z.string()).min(1, 'Phải có ít nhất 1 kỹ năng'),
  location: z.string().min(2, 'Địa điểm không hợp lệ'),
  salaryMin: z.number().optional(),
  salaryMax: z.number().optional(),
  interviewRounds: z
    .array(
      z.object({
        round: z.number().min(1),
        type: z.enum(['Screening', 'Technical']),
        duration: z.number().min(5).max(120),
      })
    )
    .min(1, 'Phải có ít nhất 1 vòng phỏng vấn'),
  interviewModes: z.array(z.enum(['Remote', 'OnSite'])).min(1),
});

export const applicationSchema = z.object({
  jobPostingId: z.string().uuid(),
  cvFile: z
    .instanceof(File)
    .refine((file) => file.size <= 10 * 1024 * 1024, 'File CV phải nhỏ hơn 10MB')
    .refine(
      (file) => ['application/pdf', 'application/msword', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'].includes(file.type),
      'Chỉ chấp nhận file PDF hoặc DOCX'
    )
    .optional(),
});

export const interviewCodeSchema = z.object({
  code: z
    .string()
    .regex(/^[A-Z0-9]{6,8}$/, 'Mã phỏng vấn phải từ 6-8 ký tự alphanumeric'),
});

export const reviewDecisionSchema = z.object({
  decision: z.enum(['confirmed', 'overridden']),
  overrideReason: z.string().optional(),
}).refine(
  (data) => data.decision === 'overridden' ? data.overrideReason && data.overrideReason.length >= 10 : true,
  { message: 'Lý do override phải có ít nhất 10 ký tự', path: ['overrideReason'] }
);

export type LoginFormData = z.infer<typeof loginSchema>;
export type JobPostingFormData = z.infer<typeof jobPostingSchema>;
export type ApplicationFormData = z.infer<typeof applicationSchema>;
export type InterviewCodeFormData = z.infer<typeof interviewCodeSchema>;
export type ReviewDecisionFormData = z.infer<typeof reviewDecisionSchema>;
