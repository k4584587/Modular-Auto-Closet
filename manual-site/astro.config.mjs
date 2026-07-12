// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

// 배포: k4584587.github.io/Modular-Auto-Closet (VCC 리스팅)의 /docs/ 서브경로
// (.github/workflows/build-listing.yml 이 빌드 결과를 Website/docs/ 로 합쳐 배포)
export default defineConfig({
	site: 'https://k4584587.github.io',
	base: '/Modular-Auto-Closet/docs',
	integrations: [
		starlight({
			title: 'Modular Auto Closet',
			customCss: ['./src/styles/custom.css'],
			defaultLocale: 'root',
			locales: {
				// root = 한국어 (URL 접두사 없음). 추후 en/ja 폴더 추가로 확장.
				root: { label: '한국어', lang: 'ko' },
			},
			social: [
				{ icon: 'github', label: 'GitHub', href: 'https://github.com/k4584587/Modular-Auto-Closet' },
			],
			sidebar: [
				{
					label: '시작하기',
					items: [
						'getting-started/intro',
						'getting-started/install',
						'getting-started/quickstart',
						'getting-started/testing',
					],
				},
				{
					label: '튜토리얼',
					items: [
						'tutorials/add-clothing',
						'tutorials/clothing-settings',
						'tutorials/create-toggle',
						'tutorials/thumbnails',
						'tutorials/manage-clothing',
						'tutorials/parameter-drivers',
					],
				},
				{
					label: '기능 레퍼런스',
					items: [
						'reference',
						'reference/autocloset',
						'reference/closet-config-component',
						'reference/standalone-components',
						'reference/context-menus',
						'reference/write-defaults',
					],
				},
				{
					label: '문제 해결',
					items: [
						'troubleshooting/common-problems',
						'troubleshooting/faq',
					],
				},
				{
					label: '기타',
					items: [
						'misc/upgrade-legacy',
						'misc/optimizers',
						'misc/changelog',
					],
				},
			],
		}),
	],
});
