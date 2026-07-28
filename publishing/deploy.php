<?php

if ( ! defined( 'WP_CLI' ) || ! WP_CLI ) {
	exit( 1 );
}

function mootop_upsert_post( $post_type, $slug, $title, $excerpt, $content_file, $parent = 0 ) {
	$matches  = get_posts(
		array(
			'post_type'      => $post_type,
			'post_status'    => 'any',
			'name'           => $slug,
			'post_parent'    => $parent,
			'posts_per_page' => 1,
		)
	);
	$existing = $matches ? $matches[0] : null;
	$post     = array(
		'post_type'    => $post_type,
		'post_status'  => 'publish',
		'post_name'    => $slug,
		'post_title'   => $title,
		'post_excerpt' => $excerpt,
		'post_content' => wp_slash( file_get_contents( $content_file ) ),
		'post_parent'  => $parent,
	);

	if ( $existing ) {
		$post['ID'] = $existing->ID;
		$result     = wp_update_post( $post, true );
	} else {
		$result = wp_insert_post( $post, true );
	}

	if ( is_wp_error( $result ) ) {
		WP_CLI::error( $result->get_error_message() );
	}

	return (int) $result;
}

$product_id = mootop_upsert_post(
	'product',
	'illustrator-typeflow',
	'Illustrator 智能输入法 v1.0.0',
	'<strong>完全免费。</strong> 在 Illustrator 2026 的画布文字、命名和参数输入之间自动调整中英文状态，让单键快捷键随手可用。',
	'/tmp/illustrator-typeflow-product.html'
);

$download = new WC_Product_Download();
$download->set_id( 'illustrator-typeflow-v1' );
$download->set_name( 'Illustrator 智能输入法 v1.0.0（Windows x64）' );
$download->set_file( '/var/lib/mootop/downloads/IllustratorTypeFlow-v1.0.0-Windows-x64.zip' );
$download->set_enabled( true );

$product = wc_get_product( $product_id );
if ( ! $product || ! is_a( $product, 'WC_Product_Simple' ) ) {
	$product = new WC_Product_Simple( $product_id );
}
$product->set_status( 'publish' );
$product->set_catalog_visibility( 'visible' );
$product->set_regular_price( '0.00' );
$product->set_price( '0.00' );
$product->set_virtual( true );
$product->set_downloadable( true );
$product->set_download_limit( -1 );
$product->set_download_expiry( -1 );
$product->set_sold_individually( true );
$product->set_manage_stock( false );
$product->set_stock_status( 'instock' );
$product->set_downloads( array( $download ) );
$product->save();

wp_set_object_terms( $product_id, array( 22 ), 'product_cat', false );
wp_set_object_terms( $product_id, array( 'Illustrator', 'Windows', '输入法工具' ), 'product_tag', false );
update_post_meta( $product_id, '_mootop_platform', 'windows' );
update_post_meta( $product_id, '_mootop_current_version', '1.0.0' );
update_post_meta( $product_id, '_mootop_product_code', 'illustrator-typeflow' );
update_post_meta( $product_id, '_mootop_license_enabled', 'no' );
update_post_meta( $product_id, '_mootop_release_stage', 'stable' );

$help_id = mootop_upsert_post(
	'page',
	'illustrator-typeflow',
	'Illustrator 智能输入法使用帮助',
	'Illustrator 智能输入法 v1.0.0 的安装、日常使用、托盘菜单、常见问题和卸载说明。',
	'/tmp/illustrator-typeflow-help.html',
	44
);

$story_id = mootop_upsert_post(
	'post',
	'illustrator-typeflow-development-notes',
	'Illustrator 智能输入法：少按一次中英切换，让脑子留在设计上',
	'从“看见输入框就开中文”的错误起点，到焦点分类、画布校正和不模拟按键的选择，记录这个托盘小工具的真实开发过程。',
	'/tmp/illustrator-typeflow-development-story.html'
);

wp_set_post_terms( $story_id, array( 26 ), 'category', false );
wp_set_post_terms(
	$story_id,
	array( 'Adobe Illustrator', 'Windows 工具', '输入法', '独立开发', '免费工具' ),
	'post_tag',
	false
);

WP_CLI::line(
	wp_json_encode(
		array(
			'product_id' => $product_id,
			'help_id'    => $help_id,
			'story_id'   => $story_id,
		),
		JSON_UNESCAPED_UNICODE
	)
);
